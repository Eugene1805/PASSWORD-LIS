using Data.DAL.Interfaces;
using Data.Model;
using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Services.Services.Internal;
using Services.Util;
using Services.Wrappers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class GameManager : ServiceBase, IGameManager
    {
        private readonly ConcurrentDictionary<string, MatchSession> matches =
            new ConcurrentDictionary<string, MatchSession>();
        private readonly IOperationContextWrapper operationContext;
        private readonly IWordRepository wordRepository;
        private readonly IMatchRepository matchRepository;
        private readonly IPlayerRepository playerRepository;
        private static readonly ILog log = LogManager.GetLogger(typeof(GameManager));

        private const int RoundDurationSeconds = 30;
        private const int ValidationDurationSeconds = 30;
        private const int SuddenDeathDurationSeconds = 30;
        private const int WordsPerRound = 2;
        private const int TotalRounds = 2;
        private const int PointsPerWin = 10;
        private const int PlayersPerMatch = 4;
        private const int SuddenDeathWordsBuffer = 15;

        public GameManager(IOperationContextWrapper contextWrapper, IWordRepository wordRepository,
            IMatchRepository matchRepository, IPlayerRepository playerRepository) : base(log)
        {
            this.operationContext = contextWrapper;
            this.wordRepository = wordRepository;
            this.matchRepository = matchRepository;
            this.playerRepository = playerRepository;
        }

        public bool CreateMatch(string gameCode, List<PlayerDTO> playersFromWaitingRoom)
        {
            return Execute(() =>
            {
                if (playersFromWaitingRoom == null || playersFromWaitingRoom.Count != PlayersPerMatch)
                {
                    return false;
                }
                var matchSession = new MatchSession(gameCode, playersFromWaitingRoom);
                return matches.TryAdd(gameCode, matchSession);
            }, context: "GameManager: CreateMatch");
        }

        public async Task PassTurnAsync(string gameCode, int senderPlayerId)
        {
            await ExecuteAsync(async () =>
            {
                var session = GetPassTurnSession(gameCode);
                if (session == null)
                {
                    return;
                }

                var sender = GetPassTurnSender(session, senderPlayerId);
                if (sender.Player == null)
                {
                    return;
                }

                var team = sender.Player.Team;
                if (HasTeamAlreadyPassed(session, team))
                {
                    return;
                }

                var currentPassword = session.GetCurrentPassword(team);
                AddPassHistoryIfNeeded(session, team, currentPassword);

                ApplyPassAndAdvance(session, team);

                var nextWord = session.GetCurrentPassword(team);
                await SendPassTurnUpdatesAsync(session, sender, nextWord);
            }, context: "GameManager: PassTurnAsync");
        }

        public async Task SubmitClueAsync(string gameCode, int senderPlayerId, string clue)
        {
            await ExecuteAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(clue))
                {
                    return;
                }

                if (!matches.TryGetValue(gameCode, out MatchSession session)
                    || session.Status != MatchStatus.InProgress && session.Status != MatchStatus.SuddenDeath)
                {
                    return;
                }
                if (!session.ActivePlayers.TryGetValue(senderPlayerId, out var sender))
                {
                    return;
                }
                if (sender.Player.Role != PlayerRole.ClueGuy)
                {
                    return;
                }

                var team = sender.Player.Team;
                var currentPassword = session.GetCurrentPassword(team);
                if (currentPassword == null)
                {
                    return;
                }
                var historyItem = new TurnHistoryDTO
                {
                    TurnId = (team == MatchTeam.RedTeam) ?
                    session.RedTeamWordIndex : session.BlueTeamWordIndex,
                    Password = DTOMapper.ToWordDTO(currentPassword),
                    ClueUsed = clue
                };

                if (team == MatchTeam.RedTeam)
                {
                    session.RedTeamTurnHistory.Add(historyItem);
                }
                else
                {
                    session.BlueTeamTurnHistory.Add(historyItem);
                }

                var partner = session.GetPartner(sender);
                if (partner.Callback != null)
                {
                    try
                    {
                        GameBroadcaster.SendToPlayer(partner, cb => cb.OnClueReceived(clue));
                    }
                    catch
                    {
                        await HandlePlayerDisconnectionAsync(session, partner.Player.Id);
                    }
                }
            }, context: "GameManager: SubmitClueAsync");
        }

        public async Task SubmitGuessAsync(string gameCode, int senderPlayerId, string guess)
        {
            await ExecuteAsync(async () =>
            {
                if (IsInvalidGuessInput(guess))
                {
                    return;
                }

                var session = GetGuessableSessionOrNull(gameCode);
                if (session == null)
                {
                    return;
                }

                var sender = GetValidGuesser(session, senderPlayerId);
                if (sender.Player == null)
                {
                    return;
                }

                var team = sender.Player.Team;
                var currentPassword = session.GetCurrentPassword(team);
                int currentScore = (team == MatchTeam.RedTeam) ? session.RedTeamScore : session.BlueTeamScore;

                bool isCorrect = IsGuessCorrect(currentPassword, guess);

                if (isCorrect)
                {
                    await HandleCorrectGuessAsync(session, team);
                }
                else
                {
                    await HandleIncorrectGuessAsync(session, sender, team, currentScore);
                }
            }, context: "GameManager: SubmitGuessAsync");
        }

        public async Task SubmitValidationVotesAsync(string gameCode, int senderPlayerId,
            List<ValidationVoteDTO> votes)
        {
            await ExecuteAsync(async () =>
            {
                try
                {
                    log.InfoFormat("SubmitValidationVotesAsync called - GameCode: {0}, PlayerId: {1}, VotesCount: {2}",
                        gameCode, senderPlayerId, votes == null ? 0 : votes.Count);
                    if (votes == null)
                    {
                        log.WarnFormat("Votes list is null for game '{0}', player {1}", gameCode, senderPlayerId);
                        return;
                    }
                    if (!matches.TryGetValue(gameCode, out MatchSession session)
                        || session.Status != MatchStatus.Validating)
                    {
                        log.WarnFormat("Match not found or not in validating state: {0}", gameCode);
                        return;
                    }

                    if (!session.ActivePlayers.TryGetValue(senderPlayerId, out var sender))
                    {
                        log.WarnFormat("Player not found in match: {0}", senderPlayerId);
                        return;
                    }

                    bool allVotesIn = false;
                    lock (session.ReceivedVotes)
                    {
                        if (session.PlayersWhoVoted.Contains(senderPlayerId))
                        {
                            return;
                        }

                        session.PlayersWhoVoted.Add(senderPlayerId);
                        session.ReceivedVotes.Add((sender.Player.Team, votes));

                        if (session.PlayersWhoVoted.Count >= PlayersPerMatch)
                        {
                            allVotesIn = true;
                        }
                    }

                    if (allVotesIn)
                    {
                        session.StopTimers();
                        await Task.Run(async () => await ProcessVotesAsync(session));
                    }
                    log.InfoFormat("Votes processing scheduled or stored successfully for game '{0}'", gameCode);
                }
                catch (Exception ex)
                {
                    log.ErrorFormat("Error in SubmitValidationVotesAsync: {0}", ex.Message, ex);
                    log.DebugFormat("Stack Trace: {0}", ex.StackTrace);
                    throw;
                }
            }, context: "GameManager: SubmitValidationVotesAsync");
        }

        public async Task SubscribeToMatchAsync(string gameCode, int playerId)
        {
            await ExecuteAsync(async () =>
            {
                if (!matches.TryGetValue(gameCode, out MatchSession session))
                {
                    log.WarnFormat("SubscribeToMatchAsync failed - game '{0}' not found or already ended.", gameCode);
                    throw FaultExceptionFactory.Create(ServiceErrorCode.SubscriptionError,
                        "MATCH_NOT_FOUND_OR_ENDED", "The match does not exist or has already ended.");
                }
                if (session.Status != MatchStatus.WaitingForPlayers)
                {
                    log.WarnFormat("SubscribeToMatchAsync failed - game '{0}' already started or finishing.", gameCode);
                    throw FaultExceptionFactory.Create(ServiceErrorCode.SubscriptionError,
                        "MATCH_ALREADY_STARTED_OR_FINISHING", "The match has already started or is finishing.");
                }
                var expectedPlayer = session.ExpectedPlayers.FirstOrDefault(p => p.Id == playerId);
                if (expectedPlayer == null)
                {
                    log.WarnFormat("SubscribeToMatchAsync unauthorized join attempt for player {0} in game '{1}'.",
                        playerId, gameCode);
                    throw FaultExceptionFactory.Create(ServiceErrorCode.SubscriptionError,
                        "NOT_AUTHORIZED_TO_JOIN", "You are not authorized to join this match.");
                }
                if (session.ActivePlayers.ContainsKey(playerId))
                {
                    log.WarnFormat("SubscribeToMatchAsync duplicate join for player {0} in game '{1}'.",
                        playerId, gameCode);
                    throw FaultExceptionFactory.Create(ServiceErrorCode.AlreadyInRoom,
                        "PLAYER_ALREADY_IN_MATCH", "Player is already in the match.");
                }
                var callback = operationContext.GetCallbackChannel<IGameManagerCallback>();
                if (!session.ActivePlayers.TryAdd(playerId, (callback, expectedPlayer)))
                {
                    log.ErrorFormat("SubscribeToMatchAsync internal error adding player {0} to game '{1}'.",
                        playerId, gameCode);
                    throw FaultExceptionFactory.Create(ServiceErrorCode.SubscriptionError,
                        "JOIN_INTERNAL_ERROR", "Internal error while joining the match.");
                }
                if (session.ActivePlayers.Count == session.ExpectedPlayers.Count)
                {
                    await StartGameInternalAsync(session);
                }
            }, context: "GameManager: SubscribeToMatchAsync");
        }

        // Private methods (unchanged)
        private async Task StartGameInternalAsync(MatchSession session)
        {
            try
            {
                int regularWordsCount = TotalRounds * WordsPerRound;
                int totalWordsToObtain = regularWordsCount + SuddenDeathWordsBuffer;

                session.AllRedWords = await wordRepository.GetRandomWordsAsync(totalWordsToObtain);
                session.AllBlueWords = await wordRepository.GetRandomWordsAsync(totalWordsToObtain);

                if (session.AllRedWords.Count < totalWordsToObtain || session.AllBlueWords.Count < totalWordsToObtain)
                {
                    await BroadcastAndHandleDisconnectsAsync(session,
                        cb => cb.OnMatchCancelled("Error not enough words found in the database."));
                    matches.TryRemove(session.GameCode, out _);
                    return;
                }

                var initState = new MatchInitStateDTO
                {
                    Players = session.ActivePlayers.Values.Select(p => p.Player).ToList()
                };
                await BroadcastAndHandleDisconnectsAsync(session, callback => callback.OnMatchInitialized(initState));
                await StartNewRoundAsync(session);
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Error starting match {0} \n {1}", session.GameCode, ex);
                await BroadcastAndHandleDisconnectsAsync(session,
                    callback => callback.OnMatchCancelled("Error starting the match."));
                matches.TryRemove(session.GameCode, out _);
                throw;
            }
        }

        private async Task StartNewRoundAsync(MatchSession session)
        {
            session.Status = MatchStatus.InProgress;
            session.CurrentRound++;

            if (session.CurrentRound > 1)
            {
                foreach (var playerEntry in session.ActivePlayers.Values)
                {
                    playerEntry.Player.Role = (playerEntry.Player.Role == PlayerRole.ClueGuy)
                        ? PlayerRole.Guesser : PlayerRole.ClueGuy;
                }
            }

            var roundStartState = new RoundStartStateDTO
            {
                CurrentRound = session.CurrentRound,
                PlayersWithNewRoles = session.ActivePlayers.Select(playerEntry => playerEntry.Value.Player).ToList()
            };
            await BroadcastAndHandleDisconnectsAsync(session, cb => cb.OnNewRoundStarted(roundStartState));

            session.LoadWordsForRound(WordsPerRound);

            session.RedTeamTurnHistory.Clear();
            session.BlueTeamTurnHistory.Clear();
            session.RedTeamPassedThisRound = false;
            session.BlueTeamPassedThisRound = false;

            session.StartRoundTimer(TimerTickCallback, session, RoundDurationSeconds);

            await DistributeInitialWords(session, MatchTeam.RedTeam);
            await DistributeInitialWords(session, MatchTeam.BlueTeam);
        }

        private async Task DistributeInitialWords(MatchSession session, MatchTeam team)
        {
            var clueGuy = session.GetPlayerByRole(team, PlayerRole.ClueGuy);
            var guesser = session.GetPlayerByRole(team, PlayerRole.Guesser);
            var word = session.GetCurrentPassword(team);

            try
            {
                if (clueGuy.Callback != null)
                {
                    GameBroadcaster.SendToPlayer(clueGuy, cb => cb.OnNewPassword(DTOMapper.ToWordDTO(word)));
                }

                if (guesser.Callback != null)
                {
                    GameBroadcaster.SendToPlayer(guesser, cb => cb.OnNewPassword(DTOMapper.ToMaskedWordDTO(word)));
                }
            }
            catch
            {
                if (clueGuy.Player != null)
                {
                    await HandlePlayerDisconnectionAsync(session, clueGuy.Player.Id);
                }
                if (guesser.Player != null)
                {
                    await HandlePlayerDisconnectionAsync(session, guesser.Player.Id);
                }
                throw;
            }
        }

        private async void TimerTickCallback(object state)
        {
            var session = (MatchSession)state;
            if (session.Status != MatchStatus.InProgress && session.Status != MatchStatus.SuddenDeath)
            {
                return;
            }
            int newTime = session.DecrementSecondsLeft();
            await BroadcastAndHandleDisconnectsAsync(session, cb => cb.OnTimerTick(newTime));
            if (newTime <= 0)
            {
                if (session.Status == MatchStatus.SuddenDeath)
                {
                    await StartSuddenDeathAsync(session);
                }
                else
                {
                    await StartValidationPhaseAsync(session);
                }
            }
        }

        private async Task StartValidationPhaseAsync(MatchSession session)
        {
            session.Status = MatchStatus.Validating;
            session.ReceivedVotes.Clear();
            session.PlayersWhoVoted.Clear();

            var redPlayers = session.GetPlayersByTeam(MatchTeam.RedTeam);
            var bluePlayers = session.GetPlayersByTeam(MatchTeam.BlueTeam);

            if (session.RedTeamTurnHistory.Count > 0)
            {
                await GameBroadcaster.BroadcastToGroupAsync(bluePlayers,
                    cb => cb.OnBeginRoundValidation(session.RedTeamTurnHistory));
            }
            if (session.BlueTeamTurnHistory.Count > 0)
            {
                await GameBroadcaster.BroadcastToGroupAsync(redPlayers,
                    cb => cb.OnBeginRoundValidation(session.BlueTeamTurnHistory));
            }
            if (session.RedTeamTurnHistory.Count == 0 && session.BlueTeamTurnHistory.Count == 0)
            {
                await ProcessVotesAsync(session);
                return;
            }
            session.StartValidationTimer(ValidationTimerTickCallback, session, ValidationDurationSeconds);
        }

        private async void ValidationTimerTickCallback(object state)
        {
            var session = (MatchSession)state;
            if (session.Status != MatchStatus.Validating)
            {
                return;
            }
            int newTime = session.DecrementValidationSecondsLeft();
            await BroadcastAndHandleDisconnectsAsync(session, cb => cb.OnValidationTimerTick(newTime));

            if (newTime <= 0)
            {
                session.StopTimers();
                await ProcessVotesAsync(session);
            }
        }

        private async Task ProcessVotesAsync(MatchSession session)
        {
            try
            {
                session.StopTimers();

                var (redPenalty, bluePenalty) = RulesEngine.CalculateValidationPenalties(session.ReceivedVotes);

                session.ApplyPenalties(redPenalty, bluePenalty);

                var validationResult = new ValidationResultDTO
                {
                    TotalPenaltyApplied = redPenalty + bluePenalty,
                    NewRedTeamScore = session.RedTeamScore,
                    NewBlueTeamScore = session.BlueTeamScore,
                };
                log.InfoFormat("Before BroadcastAsync - sending ValidationComplete for game '{0}'",
                    session.GameCode);
                await BroadcastAndHandleDisconnectsAsync(session, cb => cb.OnValidationComplete(validationResult));
                if (session.CurrentRound >= TotalRounds)
                {
                    await EndGameAsync(session);
                }
                else
                {
                    await StartNewRoundAsync(session);
                }
                log.InfoFormat("ProcessVotesAsync completed successfully for game '{0}'", session.GameCode);
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Error in ProcessVotesAsync: {0}", ex.Message, ex);
                throw;
            }
        }

        private async Task EndGameAsync(MatchSession session)
        {
            if (session.RedTeamScore == session.BlueTeamScore)
            {
                await StartSuddenDeathAsync(session);
                return;
            }
            session.Status = MatchStatus.Finished;
            MatchTeam? winner = (session.RedTeamScore > session.BlueTeamScore)
                ? MatchTeam.RedTeam : MatchTeam.BlueTeam;
            await PersistAndNotifyGameEnd(session, winner);
        }

        private async Task StartSuddenDeathAsync(MatchSession session)
        {
            session.Status = MatchStatus.SuddenDeath;

            await BroadcastAsync(session, cb => cb.OnSuddenDeathStarted());

            int wordsUsedInRegularRounds = TotalRounds * WordsPerRound;

            bool succes = session.LoadNextSuddenDeathWord(wordsUsedInRegularRounds);
            if (!succes)
            {
                await BroadcastAndHandleDisconnectsAsync(session,
                    cb => cb.OnMatchCancelled("Error: Sudden death words exhausted."));
                matches.TryRemove(session.GameCode, out _);
                return;
            }

            session.RedTeamTurnHistory.Clear();
            session.BlueTeamTurnHistory.Clear();
            session.RedTeamPassedThisRound = true;
            session.BlueTeamPassedThisRound = true;

            session.StartRoundTimer(TimerTickCallback, session, SuddenDeathDurationSeconds);

            await DistributeInitialWords(session, MatchTeam.RedTeam);
            await DistributeInitialWords(session, MatchTeam.BlueTeam);
        }

        private async Task PersistAndNotifyGameEnd(MatchSession session, MatchTeam? winner)
        {
            var registeredRedPlayerIds = session.GetPlayersByTeam(MatchTeam.RedTeam)
                .Where(p => p.Player.Id > 0).Select(p => p.Player.Id).ToList();
            var registeredBluePlayerIds = session.GetPlayersByTeam(MatchTeam.BlueTeam)
                .Where(p => p.Player.Id > 0).Select(p => p.Player.Id).ToList();
            try
            {
                await matchRepository.SaveMatchResultAsync(session.RedTeamScore, session.BlueTeamScore,
                    registeredRedPlayerIds, registeredBluePlayerIds);
                if (winner.HasValue)
                {
                    var winningPlayerIds = (winner == MatchTeam.RedTeam)
                        ? registeredRedPlayerIds : registeredBluePlayerIds;
                    if (winningPlayerIds.Any())
                    {
                        await Task.WhenAll(winningPlayerIds.Select(
                            id => playerRepository.UpdatePlayerTotalPointsAsync(id, PointsPerWin)));
                    }
                }
            }
            catch (Exception ex)
            {
                log.ErrorFormat("ERROR persisting match {0}: {1}", session.GameCode, ex.Message);
            }
            var summary = new MatchSummaryDTO
            {
                WinnerTeam = winner,
                RedScore = session.RedTeamScore,
                BlueScore = session.BlueTeamScore
            };
            await BroadcastAndHandleDisconnectsAsync(session, cb => cb.OnMatchOver(summary));
            if (matches.TryRemove(session.GameCode, out var removedMatch))
            {
                removedMatch.Dispose();
            }
        }

        private async Task BroadcastAndHandleDisconnectsAsync(MatchSession session, Action<IGameManagerCallback> action)
        {
            var disconnectedIds = await GameBroadcaster.BroadcastAsync(session, action);

            foreach (var playerId in disconnectedIds)
            {
                log.InfoFormat("Processing disconnection for player {0} found during broadcast.", playerId);
                await HandlePlayerDisconnectionAsync(session, playerId);
            }
        }

        private async Task HandlePlayerDisconnectionIfFailed(MatchSession session, int playerId)
        {
            if (session.ActivePlayers.ContainsKey(playerId))
            {
                await HandlePlayerDisconnectionAsync(session, playerId);
            }
        }

        private async Task HandlePlayerDisconnectionAsync(MatchSession session, int disconnectedPlayerId)
        {
            if (session.Status == MatchStatus.Finished)
            {
                return;
            }

            if (session.ActivePlayers.TryRemove(disconnectedPlayerId, out var disconnectedPlayer))
            {
                session.Status = MatchStatus.Finished;
                session.StopTimers();

                await BroadcastAndHandleDisconnectsAsync(session, cb => cb.OnMatchCancelled(
                    string.Format("Player {0} has disconnected.", disconnectedPlayer.Player.Nickname)));

                matches.TryRemove(session.GameCode, out _);
            }
        }

        private static async Task BroadcastAsync(MatchSession session, Action<IGameManagerCallback> action)
        {
            try
            {
                var disconnectedPlayers = new ConcurrentBag<int>();
                var tasks = session.ActivePlayers.Select(async playerEntry =>
                {
                    try
                    {
                        action(playerEntry.Value.Callback);
                        await Task.CompletedTask;
                    }
                    catch (CommunicationException ex)
                    {
                        log.WarnFormat("CommunicationException in callback for player {0}: {1}",
                            playerEntry.Key, ex.Message);
                        disconnectedPlayers.Add(playerEntry.Key);
                    }
                    catch (TimeoutException ex)
                    {
                        log.WarnFormat("TimeoutException in callback for player {0}: {1}",
                            playerEntry.Key, ex.Message);
                        disconnectedPlayers.Add(playerEntry.Key);
                    }
                    catch (Exception ex)
                    {
                        var msg = string.Format("Unexpected error in callback for player {0}: {1}",
                            playerEntry.Key, ex.Message);
                        log.Error(msg, ex);
                        disconnectedPlayers.Add(playerEntry.Key);
                    }
                });

                await Task.WhenAll(tasks);
                foreach (var playerId in disconnectedPlayers)
                {
                    if (session.ActivePlayers.TryRemove(playerId, out _))
                    {
                        log.InfoFormat("Successfully removed disconnected player: {0}", playerId);
                    }
                }
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Error in BroadcastAsync: {0}", ex.Message, ex);
                throw;
            }
        }

        private static bool IsInvalidGuessInput(string guess)
        {
            return string.IsNullOrWhiteSpace(guess);
        }

        private MatchSession GetPassTurnSession(string gameCode)
        {
            if (!matches.TryGetValue(gameCode, out MatchSession session) || session.Status != MatchStatus.InProgress)
            {
                return null;
            }
            return session;
        }
        private static (IGameManagerCallback Callback, PlayerDTO Player) GetPassTurnSender(
            MatchSession session, int playerId)
        {
            if (!session.ActivePlayers.TryGetValue(playerId, out var sender))
            {
                return default((IGameManagerCallback, PlayerDTO));
            }
            if (sender.Player.Role != PlayerRole.ClueGuy)
            {
                return default((IGameManagerCallback, PlayerDTO));
            }
            return sender;
        }
        private static bool HasTeamAlreadyPassed(MatchSession session, MatchTeam team)
        {
            if ((team == MatchTeam.RedTeam && session.RedTeamPassedThisRound) ||
                (team == MatchTeam.BlueTeam && session.BlueTeamPassedThisRound))
            {
                return true;
            }
            return false;
        }
        private static void AddPassHistoryIfNeeded(MatchSession session, MatchTeam team, PasswordWord currentPassword)
        {
            if (currentPassword != null && currentPassword.Id != -1)
            {
                var historyItem = new TurnHistoryDTO
                {
                    TurnId = (team == MatchTeam.RedTeam) ? session.RedTeamWordIndex : session.BlueTeamWordIndex,
                    Password = DTOMapper.ToWordDTO(currentPassword),
                    ClueUsed = "[]"
                };
                if (team == MatchTeam.RedTeam)
                {
                    session.RedTeamTurnHistory.Add(historyItem);
                }
                else
                {
                    session.BlueTeamTurnHistory.Add(historyItem);
                }
            }
        }
        private static void ApplyPassAndAdvance(MatchSession session, MatchTeam team)
        {
            if (team == MatchTeam.RedTeam)
            {
                session.RedTeamPassedThisRound = true;
                session.RedTeamWordIndex++;
            }
            else
            {
                session.BlueTeamPassedThisRound = true;
                session.BlueTeamWordIndex++;
            }
        }
        private async Task SendPassTurnUpdatesAsync(MatchSession session,
            (IGameManagerCallback Callback, PlayerDTO Player) sender, PasswordWord nextWord)
        {
            try
            {
                GameBroadcaster.SendToPlayer(sender, cb => cb.OnNewPassword(DTOMapper.ToWordDTO(nextWord)));
            }
            catch
            {
                await HandlePlayerDisconnectionAsync(session, sender.Player.Id);
                await HandlePlayerDisconnectionAsync(session, sender.Player.Id);
            }

            var partner = session.GetPartner(sender);
            if (partner.Callback != null)
            {
                try
                {
                    GameBroadcaster.SendToPlayer(partner, cb =>
                    {
                        cb.OnNewPassword(DTOMapper.ToMaskedWordDTO(nextWord));
                        cb.OnClueReceived("Your partner passed the word.");
                    });
                }
                catch
                {
                    await HandlePlayerDisconnectionAsync(session, partner.Player.Id);
                }
            }
        }

        private MatchSession GetGuessableSessionOrNull(string gameCode)
        {
            if (matches.TryGetValue(gameCode, out var session) &&
                (session.Status == MatchStatus.InProgress || session.Status == MatchStatus.SuddenDeath))
            {
                return session;
            }
            return null;
        }
        private static (IGameManagerCallback Callback, PlayerDTO Player) GetValidGuesser(MatchSession session, int playerId)
        {
            var sender = session.GetPlayerById(playerId);
            if (sender.Player == null || sender.Player.Role != PlayerRole.Guesser)
            {
                return default((IGameManagerCallback, PlayerDTO));
            }
            return sender;
        }
        private static bool IsGuessCorrect(PasswordWord currentPassword, string guess)
        {
            if (currentPassword == null)
            {
                return false;
            }
            return guess.Equals(currentPassword.EnglishWord, StringComparison.OrdinalIgnoreCase) ||
                   guess.Equals(currentPassword.SpanishWord, StringComparison.OrdinalIgnoreCase);
        }
        private async Task HandleCorrectGuessAsync(MatchSession session, MatchTeam team)
        {
            if (session.Status == MatchStatus.SuddenDeath)
            {
                session.Status = MatchStatus.Finished;
                session.StopTimers();
                session.AddScore(team);
                await PersistAndNotifyGameEnd(session, team);
                return;
            }
            session.AddScore(team);

            if (team == MatchTeam.RedTeam)
            {
                session.RedTeamWordIndex++;
            }
            else
            {
                session.BlueTeamWordIndex++;
            }

            int newScore = (team == MatchTeam.RedTeam) ? session.RedTeamScore : session.BlueTeamScore;
            var resultDto = new GuessResultDTO { IsCorrect = true, Team = team, NewScore = newScore };
            await BroadcastAndHandleDisconnectsAsync(session, cb => cb.OnGuessResult(resultDto));

            var nextWord = session.GetCurrentPassword(team);
            var clueGuy = session.GetPlayerByRole(team, PlayerRole.ClueGuy);
            var guesser = session.GetPlayerByRole(team, PlayerRole.Guesser);
            try
            {
                if (clueGuy.Callback != null)
                {
                    GameBroadcaster.SendToPlayer(clueGuy, cb => cb.OnNewPassword(DTOMapper.ToWordDTO(nextWord)));
                }
                if (guesser.Callback != null)
                {
                    GameBroadcaster.SendToPlayer(guesser, cb => cb.OnNewPassword(DTOMapper.ToMaskedWordDTO(nextWord)));
                }
            }
            catch
            {
                if (clueGuy.Callback != null)
                {
                    await HandlePlayerDisconnectionIfFailed(session, clueGuy.Player.Id);
                }
                if (guesser.Callback != null)
                {
                    await HandlePlayerDisconnectionIfFailed(session, guesser.Player.Id);
                }
            }
        }
        private async Task HandleIncorrectGuessAsync(MatchSession session, (IGameManagerCallback Callback, PlayerDTO Player) sender, MatchTeam team, int currentScore)
        {
            var resultDto = new GuessResultDTO { IsCorrect = false, Team = team, NewScore = currentScore };
            var clueGuy = session.GetPlayerByRole(team, PlayerRole.ClueGuy);
            try
            {
                GameBroadcaster.SendToPlayer(sender, cb => cb.OnGuessResult(resultDto));
                if (clueGuy.Callback != null)
                {
                    GameBroadcaster.SendToPlayer(clueGuy, cb => cb.OnGuessResult(resultDto));
                }
            }
            catch
            {
                await HandlePlayerDisconnectionIfFailed(session, sender.Player.Id);
                if (clueGuy.Player != null)
                {
                    await HandlePlayerDisconnectionIfFailed(session, clueGuy.Player.Id);
                }
            }
        }
    }
}