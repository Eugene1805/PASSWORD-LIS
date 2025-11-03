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
        private static int guestIdCounter =0;
        private const int MaxPlayersPerGame =4;
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
                // Rare collision, try again
                log.WarnFormat("Game code collision for '{0}', retrying game creation for email '{1}'.", gameCode, email);
                return await CreateGameAsync(email);
            }

            var playerId = await JoinGameAsRegisteredPlayerAsync(gameCode, email);
            if (playerId >0)
            {
                newGame.HostPlayerId = playerId;
                log.InfoFormat("Game '{0}' created. Host player id: {1}.", gameCode, playerId);
                return gameCode;
            }

            games.TryRemove(gameCode, out _);
            var errorDetail = new ServiceErrorDetailDTO
            {
                Code = ServiceErrorCode.CouldNotCreateRoom,
                ErrorCode = "COULD_NOT_CREATE_ROOM",
                Message = "Could not create room."
            };
            log.ErrorFormat("Failed to create game for email '{0}'. Returning fault COULD_NOT_CREATE_ROOM.", email);
            throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message));
        }
        public async Task<int> JoinGameAsRegisteredPlayerAsync(string gameCode, string email)
        {
            if (!games.TryGetValue(gameCode, out var game))
            {
                var notFound = new ServiceErrorDetailDTO { Code = ServiceErrorCode.RoomNotFound, ErrorCode = "ROOM_NOT_FOUND", Message = "The room does not exist." };
                log.WarnFormat("Join as registered failed: game '{0}' not found for email '{1}'.", gameCode, email);
                throw new FaultException<ServiceErrorDetailDTO>(notFound, new FaultReason(notFound.Message));
            }
            if (game.Players.Count >= MaxPlayersPerGame)
            {
                var full = new ServiceErrorDetailDTO { Code = ServiceErrorCode.RoomFull, ErrorCode = "ROOM_FULL", Message = "Could not join room. The room is full." };
                log.WarnFormat("Join as registered failed: room '{0}' is full for email '{1}'.", gameCode, email);
                throw new FaultException<ServiceErrorDetailDTO>(full, new FaultReason(full.Message));
            }

            var playerEntity = repository.GetPlayerByEmail(email);
            if (playerEntity == null)
            {
                var notFoundPlayer = new ServiceErrorDetailDTO { Code = ServiceErrorCode.PlayerNotFound, ErrorCode = "PLAYER_NOT_FOUND", Message = "The player was not found in the database." };
                log.WarnFormat("Join as registered failed: player not found for email '{0}' in game '{1}'.", email, gameCode);
                throw new FaultException<ServiceErrorDetailDTO>(notFoundPlayer, new FaultReason(notFoundPlayer.Message));
            }

            var playerDto = new PlayerDTO
            {
                Id = playerEntity.Id,
                PhotoId = playerEntity.UserAccount.PhotoId ??0,
                Nickname = playerEntity.UserAccount.Nickname
            };

            AssignTeamAndRole(playerDto, game.Players.Count);
            if (game.Players.ContainsKey(playerDto.Id))
            {
                var alreadyIn = new ServiceErrorDetailDTO { Code = ServiceErrorCode.AlreadyInRoom, ErrorCode = "ALREADY_IN_ROOM", Message = "Player is already in the room." };
                log.WarnFormat("Join as registered failed: player {0} already in room '{1}'.", playerDto.Id, gameCode);
                throw new FaultException<ServiceErrorDetailDTO>(alreadyIn, new FaultReason(alreadyIn.Message));
            }

            var success = await TryAddPlayerAsync(game, playerDto);
            if (!success)
            {
                log.WarnFormat("Join as registered failed to add player {0} to room '{1}'.", playerDto.Id, gameCode);
            }
            else
            {
                log.InfoFormat("Player {0} joined room '{1}' as registered.", playerDto.Id, gameCode);
            }
            return success ? playerDto.Id : -1;
        }
        public async Task<bool> JoinGameAsGuestAsync(string gameCode, string nickname)
        {
            if (!games.TryGetValue(gameCode, out var game))
            {
                var notFound = new ServiceErrorDetailDTO { Code = ServiceErrorCode.RoomNotFound, ErrorCode = "ROOM_NOT_FOUND", Message = "The room does not exist." };
                log.WarnFormat("Join as guest failed: game '{0}' not found for nickname '{1}'.", gameCode, nickname);
                throw new FaultException<ServiceErrorDetailDTO>(notFound, new FaultReason(notFound.Message));
            }
            if (game.Players.Count >= MaxPlayersPerGame)
            {
                var full = new ServiceErrorDetailDTO { Code = ServiceErrorCode.RoomFull, ErrorCode = "ROOM_FULL", Message = "Could not join room. The room is full." };
                log.WarnFormat("Join as guest failed: room '{0}' is full for nickname '{1}'.", gameCode, nickname);
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
                log.WarnFormat("Join as guest failed: generated guest id {0} already in room '{1}'.", playerDto.Id, gameCode);
                throw new FaultException<ServiceErrorDetailDTO>(alreadyIn, new FaultReason(alreadyIn.Message));
            }

            var added = await TryAddPlayerAsync(game, playerDto);
            if (added)
            {
                log.InfoFormat("Guest '{0}' (id {1}) joined room '{2}'.", nickname, playerDto.Id, gameCode);
            }
            else
            {
                log.WarnFormat("Join as guest failed to add guest id {0} to room '{1}'.", playerDto.Id, gameCode);
            }
            return added;
        }

        public async Task LeaveGameAsync(string gameCode, int playerId)
        {
            if (!games.TryGetValue(gameCode, out var game))
            {
                log.DebugFormat("LeaveGame ignored: game '{0}' not found for player {1}.", gameCode, playerId);
                return;
            }

            if (game.HostPlayerId == playerId)
            {
                log.InfoFormat("Host (player {0}) leaving room '{1}'. Notifying clients.", playerId, gameCode);
                await HostLeftAsync(gameCode);
                return;
            }

            if (game.Players.TryRemove(playerId, out _))
            {
                log.InfoFormat("Player {0} left room '{1}'. Notifying others.", playerId, gameCode);
                await BroadcastAsync(game, client => client.Item1.OnPlayerLeft(playerId));

                if (game.Players.IsEmpty)
                {
                    games.TryRemove(gameCode, out _);
                    log.InfoFormat("Room '{0}' removed after last player left.", gameCode);
                }
            }
            else
            {
                log.DebugFormat("LeaveGame: player {0} not found in room '{1}'.", playerId, gameCode);
            }
        }

        public async Task SendMessageAsync(string gameCode, ChatMessageDTO message)
        {
            if (games.TryGetValue(gameCode, out var game))
            {
                await BroadcastAsync(game, client => client.Item1.OnMessageReceived(message));
            }
            else
            {
                log.DebugFormat("SendMessage ignored: game '{0}' not found.", gameCode);
            }
        }

        public async Task StartGameAsync(string gameCode)
        {
            if (games.TryRemove(gameCode, out var game))
            {
                log.InfoFormat("Starting game for room '{0}'. Notifying clients and removing room.", gameCode);
                await BroadcastAsync(game, client => client.Item1.OnGameStarted());
            }
            else
            {
                log.DebugFormat("StartGame ignored: room '{0}' not found.", gameCode);
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
                log.InfoFormat("HostLeft for room '{0}'. Notifying clients and removing room.", gameCode);
                await BroadcastAsync(game, client => client.Item1.OnHostLeft());

                games.TryRemove(gameCode, out _);
            }
            else
            {
                log.DebugFormat("HostLeft ignored: room '{0}' not found.", gameCode);
            }
        }
        private async Task<bool> TryAddPlayerAsync(Game game, PlayerDTO player)
        {
            var callback = operationContext.GetCallbackChannel<IWaitingRoomCallback>();

            if (!game.Players.TryAdd(player.Id, (callback, player)))
            {
                log.WarnFormat("TryAddPlayer failed: could not add player {0} to room '{1}'.", player.Id, game.GameCode);
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
                        throw new TimeoutException("Callback to player timed out.");
                    }
                }
                catch (CommunicationObjectFaultedException ex)
                {
                    log.WarnFormat("Callback channel faulted for player {0}. Removing from game {1}.", playerEntry.Key, game.GameCode);
                    log.Warn("Callback channel faulted exception.", ex);
                    await LeaveGameAsync(game.GameCode, playerEntry.Key);
                }
                catch (CommunicationException ex)
                {
                    log.WarnFormat("Communication error when notifying player {0}. Removing from game {1}.", playerEntry.Key, game.GameCode);
                    log.Warn("Communication exception.", ex);
                    await LeaveGameAsync(game.GameCode, playerEntry.Key);
                }
                catch (ObjectDisposedException ex)
                {
                    log.WarnFormat("Callback disposed for player {0}. Removing from game {1}.", playerEntry.Key, game.GameCode);
                    log.Warn("Callback disposed exception.", ex);
                    await LeaveGameAsync(game.GameCode, playerEntry.Key);
                }
                catch (TimeoutException ex)
                {
                    log.WarnFormat("Timeout notifying player {0}. Removing from game {1}.", playerEntry.Key, game.GameCode);
                    log.Warn("Callback timeout exception.", ex);
                    await LeaveGameAsync(game.GameCode, playerEntry.Key);
                }
                catch (Exception ex)
                {
                    log.ErrorFormat("Unexpected error broadcasting to player {0} in game {1}.", playerEntry.Key, game.GameCode);
                    log.Error("Unexpected broadcast exception.", ex);
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
                return new string(Enumerable.Repeat(chars,5)
                    .Select(s => chars[random.Next(chars.Length)]).ToArray());
            }
        }
    }
}
