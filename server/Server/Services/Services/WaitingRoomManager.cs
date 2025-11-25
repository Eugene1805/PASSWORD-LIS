using Data.DAL.Interfaces;
using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Services.Util;
using Services.Wrappers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class WaitingRoomManager : IWaitingRoomManager
    {
        sealed class Room
        {
            public string GameCode { get; set; }
            public int HostPlayerId { get; set; }
            public ConcurrentDictionary<int, (IWaitingRoomCallback, PlayerDTO)> Players { get; } =
                new ConcurrentDictionary<int, (IWaitingRoomCallback, PlayerDTO)>();
            public object Lock { get; } = new object();
        }

        private static readonly ILog log = LogManager.GetLogger(typeof(WaitingRoomManager));

        private readonly ConcurrentDictionary<string, Room> rooms = new ConcurrentDictionary<string, Room>();
        private readonly IPlayerRepository repository;
        private readonly IOperationContextWrapper operationContext;
        private readonly IGameManager gameManager;
        private readonly IAccountRepository accountRepository;
        private readonly INotificationService notificationService;
        private static int guestIdCounter = 0;
        private const int MaxPlayersPerGame = 4;
        private static readonly Random random = new Random();
        private static readonly TimeSpan CallbackTimeout = TimeSpan.FromSeconds(5);

        public WaitingRoomManager(IPlayerRepository playerRepository, IOperationContextWrapper operationContextWrapper, IGameManager gameManager,
            IAccountRepository accountRepository, INotificationService notificationService)
        {
            repository = playerRepository;
            operationContext = operationContextWrapper;
            this.gameManager = gameManager;
            this.accountRepository = accountRepository;
            this.notificationService = notificationService;
        }

        public async Task<string> CreateRoomAsync(string email)
        {
            var gameCode = GenerateGameCode();
            var newGame = new Room { GameCode = gameCode };

            if (!rooms.TryAdd(gameCode, newGame))
            {
                log.WarnFormat("Game code collision for '{0}', retrying game creation for email '{1}'.", 
                    gameCode, email);
                return await CreateRoomAsync(email);
            }

            var playerId = await JoinRoomAsRegisteredPlayerAsync(gameCode, email);
            if (playerId > 0)
            {
                newGame.HostPlayerId = playerId;
                log.InfoFormat("Game '{0}' created. Host player id: {1}.", gameCode, playerId);
                return gameCode;
            }

            rooms.TryRemove(gameCode, out _);
            log.ErrorFormat("Failed to create game for email '{0}'. Returning fault COULD_NOT_CREATE_ROOM.", email);
            throw FaultExceptionFactory.Create(ServiceErrorCode.CouldNotCreateRoom, "COULD_NOT_CREATE_ROOM",
                "Could not create room.");
        }
        public async Task<int> JoinRoomAsRegisteredPlayerAsync(string gameCode, string email)
        {
            if (!rooms.TryGetValue(gameCode, out var game))
            {
                log.WarnFormat("Join as registered failed: game '{0}' not found for email '{1}'.", gameCode, email);
                throw FaultExceptionFactory.Create(ServiceErrorCode.RoomNotFound, "ROOM_NOT_FOUND", 
                    "The room does not exist.");
            }
            if (game.Players.Count >= MaxPlayersPerGame)
            {
                log.WarnFormat("Join as registered failed: room '{0}' is full for email '{1}'.", gameCode, email);
                throw FaultExceptionFactory.Create(ServiceErrorCode.RoomFull, "ROOM_FULL",
                    "Could not join room. The room is full.");
            }

            var callback = operationContext.GetCallbackChannel<IWaitingRoomCallback>();

            var playerEntity = await repository.GetPlayerByEmailAsync(email);
            if (playerEntity == null || playerEntity.Id < 0)
            {
                log.WarnFormat("Join as registered failed: player not found for email '{0}' in game '{1}'.",
                    email, gameCode);
                throw FaultExceptionFactory.Create(ServiceErrorCode.PlayerNotFound, "PLAYER_NOT_FOUND", 
                    "The player was not found in the database.");
            }

            if (game.Players.ContainsKey(playerEntity.Id))
            {
                log.WarnFormat("Join as registered failed: player {0} already in room '{1}'.", playerEntity.Id, 
                    gameCode);
                throw FaultExceptionFactory.Create(ServiceErrorCode.AlreadyInRoom, "ALREADY_IN_ROOM",
                    "Player already in the room.");
            }

            var playerDto = new PlayerDTO
            {
                Id = playerEntity.Id,
                PhotoId = playerEntity.UserAccount.PhotoId ?? 0,
                Nickname = playerEntity.UserAccount.Nickname
            };

            var success = await TryAddPlayerAsync(game, playerDto, callback);
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
        public async Task<bool> JoinRoomAsGuestAsync(string gameCode, string nickname)
        {
            if (!rooms.TryGetValue(gameCode, out var game))
            {
                log.WarnFormat("Join as guest failed: game '{0}' not found for nickname '{1}'.", gameCode, nickname);
                throw FaultExceptionFactory.Create(ServiceErrorCode.RoomNotFound, "ROOM_NOT_FOUND",
                    "The room does not exist.");
            }
            if (game.Players.Count >= MaxPlayersPerGame)
            {
                log.WarnFormat("Join as guest failed: room '{0}' is full for nickname '{1}'.", gameCode, nickname);
                throw FaultExceptionFactory.Create(ServiceErrorCode.RoomFull, "ROOM_FULL", 
                    "Could not join room. The room is full.");
            }

            var callback = operationContext.GetCallbackChannel<IWaitingRoomCallback>();

            int guestId = Interlocked.Decrement(ref guestIdCounter);

            var playerDto = new PlayerDTO
            {
                Id = guestId,
                Nickname = nickname
            };

            var added = await TryAddPlayerAsync(game, playerDto, callback);
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

        public async Task LeaveRoomAsync(string gameCode, int playerId)
        {
            if (!rooms.TryGetValue(gameCode, out var game))
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
                    rooms.TryRemove(gameCode, out _);
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
            if (rooms.TryGetValue(gameCode, out var game))
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
            if (!rooms.TryGetValue(gameCode, out var game))
            {
                throw FaultExceptionFactory.Create(ServiceErrorCode.RoomNotFound, "WAITING_ROOM_NOT_FOUND", 
                    "Waiting room not found.");
            }

            if (game.Players.Count != MaxPlayersPerGame)
            {
                throw FaultExceptionFactory.Create(ServiceErrorCode.CouldNotCreateRoom, "NOT_ENOUGH_PLAYERS",
                    "Four players are required to start a game.");
            }
            var playerList = game.Players.Values.Select(p => p.Item2).ToList();
            bool matchCreated = gameManager.CreateMatch(gameCode, playerList);

            if (!matchCreated)
            {
                throw FaultExceptionFactory.Create(ServiceErrorCode.CouldNotCreateRoom, "COULD_NOT_CREATE_GAME",
                    "Could not create the game.");
            }

            log.InfoFormat("Starting game for room '{0}'. Notifying clients and removing room.", gameCode);
            await BroadcastAsync(game, client => client.Item1.OnGameStarted());

            rooms.TryRemove(gameCode, out _);
        }

        public Task<List<PlayerDTO>> GetPlayersInRoomAsync(string gameCode)
        {
            if (rooms.TryGetValue(gameCode, out var game))
            {
                var players = game.Players.Values.Select(p => p.Item2).ToList();
                return Task.FromResult(players);
            }

            return Task.FromResult(new List<PlayerDTO>());
        }
        public async Task HostLeftAsync(string gameCode)
        {
            if (rooms.TryGetValue(gameCode, out var game))
            {
                log.InfoFormat("HostLeft for room '{0}'. Notifying clients and removing room.", gameCode);
                await BroadcastAsync(game, client => client.Item1.OnHostLeft());

                rooms.TryRemove(gameCode, out _);
            }
            else
            {
                log.DebugFormat("HostLeft ignored: room '{0}' not found.", gameCode);
            }
        }
        private async Task<bool> TryAddPlayerAsync(Room game, PlayerDTO player, IWaitingRoomCallback callback)
        {
            if (callback == null)
            {
                log.WarnFormat("TryAddPlayer failed: no callback channel for player {0} in room '{1}'.",
                    player.Id, game.GameCode);
                return false;
            }

            lock (game.Lock)
            {
                if (game.Players.ContainsKey(player.Id))
                {
                    log.WarnFormat("TryAddPlayer duplicate detected for player {0} in room '{1}'.", 
                        player.Id, game.GameCode);
                    return false;
                }

                var assigned = AssignTeamAndRole(game, player);
                if (!assigned)
                {
                    log.WarnFormat("TryAddPlayer failed: no available team/role slot for player {0} in room '{1}'.",
                        player.Id, game.GameCode);
                    return false;
                }

                if (!game.Players.TryAdd(player.Id, (callback, player)))
                {
                    log.WarnFormat("TryAddPlayer failed: could not add player {0} to room '{1}'.", player.Id, 
                        game.GameCode);
                    return false;
                }
            }
            await BroadcastAsync(game, client => client.Item1.OnPlayerJoined(player));
            return true;
        }

        private static bool AssignTeamAndRole(Room game, PlayerDTO player)
        {
            var occupied = new HashSet<(MatchTeam team, PlayerRole role)>(
                game.Players.Values.Select(p => (p.Item2.Team, p.Item2.Role))
            );

            var desired = new List<(MatchTeam team, PlayerRole role)>
            {
                (MatchTeam.RedTeam, PlayerRole.ClueGuy),
                (MatchTeam.BlueTeam, PlayerRole.ClueGuy),
                (MatchTeam.RedTeam, PlayerRole.Guesser),
                (MatchTeam.BlueTeam, PlayerRole.Guesser)
            };

            if (!desired.Any(d => !occupied.Contains(d)))
            {
                return false;
            }
            var (team, role) = desired.First(d => !occupied.Contains(d));

            player.Team = team;
            player.Role = role;
            return true;
        }

        private async Task BroadcastAsync(Room game, Action<(IWaitingRoomCallback, PlayerDTO)> action)
        {
            var snapshot = game.Players.ToArray();

            var tasks = snapshot.Select(async playerEntry =>
            {
                try
                {
                    var callTask = Task.Run(() => action(playerEntry.Value));
                    var completed = await Task.WhenAny(callTask, Task.Delay(CallbackTimeout));
                    if (completed != callTask)
                    {
                        throw new TimeoutException("Callback to player timed out.");
                    }

                    await callTask;
                }
                catch (CommunicationObjectFaultedException ex)
                {
                    log.WarnFormat("Callback channel faulted for player {0}. Removing from game {1}. \n {2}",
                        playerEntry.Key, game.GameCode, ex);
                    await LeaveRoomAsync(game.GameCode, playerEntry.Key);
                }
                catch (CommunicationException ex)
                {
                    log.WarnFormat("Communication error when notifying player {0}. Removing from game {1}. \n {2}",
                        playerEntry.Key, game.GameCode, ex);
                    await LeaveRoomAsync(game.GameCode, playerEntry.Key);
                }
                catch (ObjectDisposedException ex)
                {
                    log.WarnFormat("Callback disposed for player {0}. Removing from game {1}.\n {2}",
                        playerEntry.Key, game.GameCode, ex);
                    await LeaveRoomAsync(game.GameCode, playerEntry.Key);
                }
                catch (TimeoutException ex)
                {
                    log.WarnFormat("Timeout notifying player {0}. Removing from game {1}. \n {2}",
                        playerEntry.Key, game.GameCode, ex);
                    await LeaveRoomAsync(game.GameCode, playerEntry.Key);
                }
                catch (Exception ex)
                {
                    log.ErrorFormat("Unexpected error broadcasting to player {0} in game {1}. \n {2}",
                        playerEntry.Key, game.GameCode, ex);
                    await LeaveRoomAsync(game.GameCode, playerEntry.Key);
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

        public async Task SendGameInvitationByEmailAsync(string email, string gameCode, string inviterNickname)
        {
            try
            {
                var user = await accountRepository.GetUserByEmailAsync(email);
                if (user != null && user.Nickname != inviterNickname)
                {
                    await notificationService.SendGameInvitationEmailAsync(email, gameCode, inviterNickname);
                    log.InfoFormat("Invitation email sent to {0} for room {1} by {2}", email, gameCode,
                        inviterNickname);
                }
            }
            catch (ConfigurationErrorsException)
            {
                throw FaultExceptionFactory.Create(
                    ServiceErrorCode.EmailConfigurationError,
                    "EMAIL_CONFIGURATION_ERROR",
                    "Email service configuration error"
                );
            }
            catch (FormatException)
            {
                throw FaultExceptionFactory.Create(
                    ServiceErrorCode.EmailConfigurationError,
                    "EMAIL_SERVICE_CONFIGURATION_ERROR",
                    "Invalid email service configuration"
                );
            }
            catch (SmtpException)
            {
                throw FaultExceptionFactory.Create(
                    ServiceErrorCode.EmailSendingError,
                    "EMAIL_SENDING_ERROR",
                    "Failed to send email"
                );
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Unexpected error in SendGameInvitationByEmailAsync to {0}.\n{1}", email, ex);
                throw FaultExceptionFactory.Create(
                    ServiceErrorCode.UnexpectedError,
                    "UNEXPECTED_ERROR",
                    "Unexpected server error."
                );
            }
        }

        public async Task SendGameInvitationToFriendAsync(int friendPlayerId, string gameCode, string inviterNickname)
        {
            try
            {
                var userAccount = await accountRepository.GetUserByPlayerIdAsync(friendPlayerId);
                if (userAccount == null || string.IsNullOrEmpty(userAccount.Email))
                {
                    log.WarnFormat("SendGameInvitationToFriendAsync failed: Could not find email for PlayerId {0}",
                        friendPlayerId);
                    throw FaultExceptionFactory.Create(
                        ServiceErrorCode.PlayerNotFound,
                        "FRIEND_NOT_FOUND",
                        "Friend not found or email missing."
                    );
                }

                await notificationService.SendGameInvitationEmailAsync(userAccount.Email, gameCode, inviterNickname);
                log.InfoFormat("Invitation to friend sent to {0} (PlayerId {1}) for room {2}", userAccount.Email,
                    friendPlayerId, gameCode);
            }
            catch (ConfigurationErrorsException)
            {
                throw FaultExceptionFactory.Create(
                    ServiceErrorCode.EmailConfigurationError,
                    "EMAIL_CONFIGURATION_ERROR",
                    "Email service configuration error"
                );
            }
            catch (FormatException)
            {
                throw FaultExceptionFactory.Create(
                    ServiceErrorCode.EmailConfigurationError,
                    "EMAIL_CONFIGURATION_ERROR",
                    "Invalid email service configuration"
                );
            }
            catch (SmtpException)
            {
                log.WarnFormat("Could not send invitation to {0}", friendPlayerId);
                throw FaultExceptionFactory.Create(
                    ServiceErrorCode.EmailSendingError,
                    "EMAIL_SENDING_ERROR",
                    "Failed to send email"
                );
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Unexpected error in SendGameInvitationToFriendAsync for {0}.\n{1}", friendPlayerId, ex);
                throw FaultExceptionFactory.Create(
                    ServiceErrorCode.UnexpectedError,
                    "UNEXPECTED_ERROR",
                    "Unexpected error at SendGameInvitationToFriendAsync"
                    );
            }
        }
    }
}
