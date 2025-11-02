using Data.DAL.Interfaces;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Services.Wrappers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class WaitingRoomManager : IWaitingRoomManager
    {
        sealed class Game
        {
            public string GameCode { get; set; }
            public int HostPlayerId { get; set; }
            public ConcurrentDictionary<int, (IWaitingRoomCallback, PlayerDTO)> Players { get; } = new ConcurrentDictionary<int, (IWaitingRoomCallback, PlayerDTO)>();
        }

        private static readonly ILog log = LogManager.GetLogger(typeof(WaitingRoomManager));

        private readonly ConcurrentDictionary<string, Game> games = new ConcurrentDictionary<string, Game>();
        private readonly IPlayerRepository repository;
        private readonly IOperationContextWrapper operationContext;
        private static int guestIdCounter = 0;
        private const int MaxPlayersPerGame = 4;
        private static readonly Random random = new Random();
        private static readonly TimeSpan CallbackTimeout = TimeSpan.FromSeconds(5);

        public WaitingRoomManager(IPlayerRepository playerRepository, IOperationContextWrapper operationContextWrapper)
        {
            repository = playerRepository;
            operationContext = operationContextWrapper;
        }

        public async Task<string> CreateGameAsync(string email)
        {
            var gameCode = GenerateGameCode();
            var newGame = new Game { GameCode = gameCode };

            if (!games.TryAdd(gameCode, newGame))
            {
                return await CreateGameAsync(email);
            }

            var playerId = await JoinGameAsRegisteredPlayerAsync(gameCode, email);
            if (playerId > 0)
            {
                newGame.HostPlayerId = playerId;
                return gameCode;
            }

            games.TryRemove(gameCode, out _);
            var errorDetail = new ServiceErrorDetailDTO
            {
                Code = ServiceErrorCode.CouldNotCreateRoom,
                ErrorCode = "COULD_NOT_CREATE_ROOM",
                Message = "Could not create room."
            };
            throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message));
        }
        public async Task<int> JoinGameAsRegisteredPlayerAsync(string gameCode, string email)
        {
            if (!games.TryGetValue(gameCode, out var game))
            {
                var notFound = new ServiceErrorDetailDTO { Code = ServiceErrorCode.RoomNotFound, ErrorCode = "ROOM_NOT_FOUND", Message = "The room does not exist." };
                throw new FaultException<ServiceErrorDetailDTO>(notFound, new FaultReason(notFound.Message));
            }
            if (game.Players.Count >= MaxPlayersPerGame)
            {
                var full = new ServiceErrorDetailDTO { Code = ServiceErrorCode.RoomFull, ErrorCode = "ROOM_FULL", Message = "Could not join room. The room is full." };
                throw new FaultException<ServiceErrorDetailDTO>(full, new FaultReason(full.Message));
            }

            var playerEntity = repository.GetPlayerByEmail(email);
            if (playerEntity == null)
            {
                var notFoundPlayer = new ServiceErrorDetailDTO { Code = ServiceErrorCode.PlayerNotFound, ErrorCode = "PLAYER_NOT_FOUND", Message = "The player was not found in the database." };
                throw new FaultException<ServiceErrorDetailDTO>(notFoundPlayer, new FaultReason(notFoundPlayer.Message));
            }

            var playerDto = new PlayerDTO
            {
                Id = playerEntity.Id,
                PhotoId = playerEntity.UserAccount.PhotoId ?? 0,
                Nickname = playerEntity.UserAccount.Nickname
            };

            AssignTeamAndRole(playerDto, game.Players.Count);
            if (game.Players.ContainsKey(playerDto.Id))
            {
                var alreadyIn = new ServiceErrorDetailDTO { Code = ServiceErrorCode.AlreadyInRoom, ErrorCode = "ALREADY_IN_ROOM", Message = "Player is already in the room." };
                throw new FaultException<ServiceErrorDetailDTO>(alreadyIn, new FaultReason(alreadyIn.Message));
            }

            var success = await TryAddPlayerAsync(game, playerDto);
            return success ? playerDto.Id : -1;
        }
        public async Task<bool> JoinGameAsGuestAsync(string gameCode, string nickname)
        {
            if (!games.TryGetValue(gameCode, out var game))
            {
                var notFound = new ServiceErrorDetailDTO { Code = ServiceErrorCode.RoomNotFound, ErrorCode = "ROOM_NOT_FOUND", Message = "The room does not exist." };
                throw new FaultException<ServiceErrorDetailDTO>(notFound, new FaultReason(notFound.Message));
            }
            if (game.Players.Count >= MaxPlayersPerGame)
            {
                var full = new ServiceErrorDetailDTO { Code = ServiceErrorCode.RoomFull, ErrorCode = "ROOM_FULL", Message = "Could not join room. The room is full." };
                throw new FaultException<ServiceErrorDetailDTO>(full, new FaultReason(full.Message));
            }

            int guestId = Interlocked.Decrement(ref guestIdCounter);

            var playerDto = new PlayerDTO
            {
                Id = guestId,
                Nickname = nickname
            };

            AssignTeamAndRole(playerDto, game.Players.Count);

            if (game.Players.ContainsKey(playerDto.Id))
            {
                var alreadyIn = new ServiceErrorDetailDTO { Code = ServiceErrorCode.AlreadyInRoom, ErrorCode = "ALREADY_IN_ROOM", Message = "Player is already in the room." };
                throw new FaultException<ServiceErrorDetailDTO>(alreadyIn, new FaultReason(alreadyIn.Message));
            }

            return await TryAddPlayerAsync(game, playerDto);
        }

        public async Task LeaveGameAsync(string gameCode, int playerId)
        {
            if (!games.TryGetValue(gameCode, out var game))
            {
                return;
            }

            if (game.HostPlayerId == playerId)
            {
                await HostLeftAsync(gameCode);
                return;
            }

            if (game.Players.TryRemove(playerId, out _))
            {
                await BroadcastAsync(game, client => client.Item1.OnPlayerLeft(playerId));

                if (game.Players.IsEmpty)
                {
                    games.TryRemove(gameCode, out _);
                }
            }
        }

        public async Task SendMessageAsync(string gameCode, ChatMessageDTO message)
        {
            if (games.TryGetValue(gameCode, out var game))
            {
                await BroadcastAsync(game, client => client.Item1.OnMessageReceived(message));
            }
        }

        public async Task StartGameAsync(string gameCode)
        {
            if (games.TryRemove(gameCode, out var game))
            {
                await BroadcastAsync(game, client => client.Item1.OnGameStarted());
            }
        }

        public Task<List<PlayerDTO>> GetPlayersInGameAsync(string gameCode)
        {
            if (games.TryGetValue(gameCode, out var game))
            {
                var players = game.Players.Values.Select(p => p.Item2).ToList();
                return Task.FromResult(players);
            }

            return Task.FromResult(new List<PlayerDTO>());
        }
        public async Task HostLeftAsync(string gameCode)
        {
            if (games.TryGetValue(gameCode, out var game))
            {
                await BroadcastAsync(game, client => client.Item1.OnHostLeft());

                games.TryRemove(gameCode, out _);
            }
        }
        private async Task<bool> TryAddPlayerAsync(Game game, PlayerDTO player)
        {
            var callback = operationContext.GetCallbackChannel<IWaitingRoomCallback>();

            if (!game.Players.TryAdd(player.Id, (callback, player)))
            {
                return false;
            }

            await BroadcastAsync(game, client => client.Item1.OnPlayerJoined(player));
            return true;
        }

        private static void AssignTeamAndRole(PlayerDTO player, int playerCountInRoom)
        {
            switch (playerCountInRoom)
            {
                case 0:
                    player.Team = MatchTeam.RedTeam;
                    player.Role = PlayerRole.ClueGuy;
                    break;
                case 1:
                    player.Team = MatchTeam.BlueTeam;
                    player.Role = PlayerRole.ClueGuy;
                    break;
                case 2:
                    player.Team = MatchTeam.RedTeam;
                    player.Role = PlayerRole.Guesser;
                    break;
                case 3:
                    player.Team = MatchTeam.BlueTeam;
                    player.Role = PlayerRole.Guesser;
                    break;
            }
        }
        private async Task BroadcastAsync(Game game, Action<(IWaitingRoomCallback, PlayerDTO)> action)
        {
            // Snapshot the current players to avoid concurrent modification issues during iteration
            var snapshot = game.Players.ToArray();

            var tasks = snapshot.Select(async playerEntry =>
            {
                try
                {
                    // Execute the callback with a timeout, so misbehaving clients don't block the broadcast
                    var callTask = Task.Run(() => action(playerEntry.Value));
                    var completed = await Task.WhenAny(callTask, Task.Delay(CallbackTimeout));
                    if (completed != callTask)
                    {
                        throw new TimeoutException($"Callback to player {playerEntry.Key} timed out.");
                    }
                }
                catch (CommunicationObjectFaultedException ex)
                {
                    log.Warn($"Callback channel faulted for player {playerEntry.Key}. Removing from game {game.GameCode}.", ex);
                    await LeaveGameAsync(game.GameCode, playerEntry.Key);
                }
                catch (CommunicationException ex)
                {
                    log.Warn($"Communication error when notifying player {playerEntry.Key}. Removing from game {game.GameCode}.", ex);
                    await LeaveGameAsync(game.GameCode, playerEntry.Key);
                }
                catch (ObjectDisposedException ex)
                {
                    log.Warn($"Callback disposed for player {playerEntry.Key}. Removing from game {game.GameCode}.", ex);
                    await LeaveGameAsync(game.GameCode, playerEntry.Key);
                }
                catch (TimeoutException ex)
                {
                    log.Warn($"Timeout notifying player {playerEntry.Key}. Removing from game {game.GameCode}.", ex);
                    await LeaveGameAsync(game.GameCode, playerEntry.Key);
                }
                catch (Exception ex)
                {
                    log.Error($"Unexpected error broadcasting to player {playerEntry.Key} in game {game.GameCode}.", ex);
                    await LeaveGameAsync(game.GameCode, playerEntry.Key);
                }
            });

            await Task.WhenAll(tasks);
        }

        private static string GenerateGameCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            lock (random)
            {
                return new string(Enumerable.Repeat(chars, 5)
                    .Select(s => chars[random.Next(chars.Length)]).ToArray());
            }
        }
    }
}
