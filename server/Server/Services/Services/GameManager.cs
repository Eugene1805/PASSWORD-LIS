using Data.DAL.Interfaces;
using Data.Model;
using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Services.Wrappers;
using Services.Util;
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
    public class GameManager : IGameManager
    {
        sealed class MatchState : IDisposable
        {
            public string GameCode { get; }
            public MatchStatus Status { get; set; }
            public List<PlayerDTO> ExpectedPlayers { get; }
            public ConcurrentDictionary<int, (IGameManagerCallback Callback, PlayerDTO Player)> ActivePlayers { get; }
            public int RedTeamScore { get; set; }
            public int BlueTeamScore { get; set; }
            public Timer RoundTimer { get; set; }
            public Timer ValidationTimer { get; set; } 
            public int SecondsLeft;
            public int ValidationSecondsLeft; 
            public int CurrentRound { get; set; }

            public List<PasswordWord> RedTeamWords { get; set; }
            public List<PasswordWord> BlueTeamWords { get; set; }
            public int RedTeamWordIndex { get; set; }
            public int BlueTeamWordIndex { get; set; }
            public List<TurnHistoryDTO> RedTeamTurnHistory { get; set; }
            public List<TurnHistoryDTO> BlueTeamTurnHistory { get; set; }
            public bool RedTeamPassedThisRound { get; set; } 
            public bool BlueTeamPassedThisRound { get; set; } 
            public List<(MatchTeam VoterTeam, List<ValidationVoteDTO> Votes)> ReceivedVotes { get; }
            public HashSet<int> PlayersWhoVoted { get; }

            public MatchState(string gameCode, List<PlayerDTO> expectedPlayers)
            {
                GameCode = gameCode;
                ExpectedPlayers = expectedPlayers;
                Status = MatchStatus.WaitingForPlayers;
                ActivePlayers = new ConcurrentDictionary<int, (IGameManagerCallback, PlayerDTO)>();
                RedTeamWords = new List<PasswordWord>();
                BlueTeamWords = new List<PasswordWord>();
                RedTeamTurnHistory = new List<TurnHistoryDTO>();
                BlueTeamTurnHistory = new List<TurnHistoryDTO>();
                RedTeamWordIndex = 0;
                BlueTeamWordIndex = 0;
                RedTeamScore = 0;
                BlueTeamScore = 0;
                CurrentRound = 0;
                RedTeamPassedThisRound = false;
                BlueTeamPassedThisRound = false;
                ReceivedVotes = new List<(MatchTeam, List<ValidationVoteDTO>)>();
                PlayersWhoVoted = new HashSet<int>();
            }
            public PasswordWord GetCurrentPassword(MatchTeam team)
            {
                if (team == MatchTeam.RedTeam)
                {
                    if (RedTeamWordIndex < RedTeamWords.Count) return RedTeamWords[RedTeamWordIndex];
                }
                else
                {
                    if (BlueTeamWordIndex < BlueTeamWords.Count) return BlueTeamWords[BlueTeamWordIndex];
                }
                return null;
            }
            public void Dispose()
            {
                RoundTimer?.Dispose();
                ValidationTimer?.Dispose();
            }
        }

        private readonly ConcurrentDictionary<string, MatchState> matches = new ConcurrentDictionary<string, MatchState>();
        private readonly IOperationContextWrapper operationContext;
        private readonly IWordRepository wordRepository;
        private readonly IMatchRepository matchRepository;
        private readonly IPlayerRepository playerRepository;
        private static readonly ILog log = LogManager.GetLogger(typeof(GameManager));
        private const int RoundDurationSeconds = 120; // CAMBIADO PARA PRUEBAS de 60 a 180
        private const int ValidationDurationSeconds = 60; //Cambiado para pruebas de 20 a 60
        private const int SuddenDeathDurationSeconds = 30;
        private const int WordsPerRound = 5; // Change from 5 to 3 for testing
        private const int TotalRounds = 1; //CAMBIADO DE 5 A 1
        private const int PointsPerWin = 10;
        private const int PenaltySynonim = 2;
        private const int PenaltyMultiword = 1;

        public GameManager(IOperationContextWrapper contextWrapper, IWordRepository wordRepository, IMatchRepository matchRepository, IPlayerRepository playerRepository)
        {
            this.operationContext = contextWrapper;
            this.wordRepository = wordRepository;
            this.matchRepository = matchRepository;
            this.playerRepository = playerRepository;
        }

        public bool CreateMatch(string gameCode, List<PlayerDTO> playersFromWaitingRoom)
        {
            if (playersFromWaitingRoom == null || playersFromWaitingRoom.Count != 4) return false;
            var matchState = new MatchState(gameCode, playersFromWaitingRoom);
            return matches.TryAdd(gameCode, matchState);
        }

        public async Task PassTurnAsync(string gameCode, int senderPlayerId)
        {
            if (!matches.TryGetValue(gameCode, out MatchState matchState) || matchState.Status != MatchStatus.InProgress) return;
            if (!matchState.ActivePlayers.TryGetValue(senderPlayerId, out var sender)) return;
            if (sender.Player.Role != PlayerRole.ClueGuy) return;

            var team = sender.Player.Team;
            if ((team == MatchTeam.RedTeam && matchState.RedTeamPassedThisRound) ||
                (team == MatchTeam.BlueTeam && matchState.BlueTeamPassedThisRound))
            {
                return;
            }

            var currentPassword = matchState.GetCurrentPassword(team);
            if (currentPassword != null)
            {
                var historyItem = new TurnHistoryDTO
                {
                    TurnId = (team == MatchTeam.RedTeam) ? matchState.RedTeamWordIndex : matchState.BlueTeamWordIndex,
                    Password = ToDTO(currentPassword),
                    ClueUsed = "[PASSED]"
                };

                if (team == MatchTeam.RedTeam) matchState.RedTeamTurnHistory.Add(historyItem);
                else matchState.BlueTeamTurnHistory.Add(historyItem);
            }

            if (team == MatchTeam.RedTeam)
            {
                matchState.RedTeamPassedThisRound = true;
                matchState.RedTeamWordIndex++;
            }
            else
            {
                matchState.BlueTeamPassedThisRound = true;
                matchState.BlueTeamWordIndex++;
            }

            var nextWord = matchState.GetCurrentPassword(team);
            try { sender.Callback.OnNewPassword(ToDTO(nextWord)); }
            catch { await HandlePlayerDisconnectionAsync(matchState, sender.Player.Id); }

            var partner = GetPartner(matchState, sender);
            if (partner.Callback != null)
            {
                try 
                { 
                    partner.Callback.OnNewPassword(ToDTOForGuesser(nextWord)); 
                }
                catch 
                { 
                    await HandlePlayerDisconnectionAsync(matchState, partner.Player.Id); 
                }

                try 
                { 
                    partner.Callback.OnClueReceived("Your partner passed the word."); 
                }
                catch {
                    await HandlePlayerDisconnectionAsync(matchState, partner.Player.Id); 
                }
            }
        }


        public async Task SubmitClueAsync(string gameCode, int senderPlayerId, string clue)
        {
            if (string.IsNullOrWhiteSpace(clue)) return;

            if (!matches.TryGetValue(gameCode, out MatchState matchState) || matchState.Status != MatchStatus.InProgress) return;
            if (!matchState.ActivePlayers.TryGetValue(senderPlayerId, out var sender)) return;
            if (sender.Player.Role != PlayerRole.ClueGuy) return;

            var team = sender.Player.Team;
            var currentPassword = matchState.GetCurrentPassword(team);
            if (currentPassword == null) return;

            var historyItem = new TurnHistoryDTO
            {
                TurnId = (team == MatchTeam.RedTeam) ? matchState.RedTeamWordIndex : matchState.BlueTeamWordIndex,
                Password = ToDTO(currentPassword),
                ClueUsed = clue
            };

            if (team == MatchTeam.RedTeam) matchState.RedTeamTurnHistory.Add(historyItem);
            else matchState.BlueTeamTurnHistory.Add(historyItem);

            var partner = GetPartner(matchState, sender);
            if (partner.Callback != null)
            {
                try { partner.Callback.OnClueReceived(clue); }
                catch { await HandlePlayerDisconnectionAsync(matchState, partner.Player.Id); }
            }
        }

        public async Task SubmitGuessAsync(string gameCode, int senderPlayerId, string guess)
        {
            if (string.IsNullOrWhiteSpace(guess)) return;

            if (!matches.TryGetValue(gameCode, out MatchState matchState) || 
                (matchState.Status != MatchStatus.InProgress && matchState.Status != MatchStatus.SuddenDeath)) return;
            if (!matchState.ActivePlayers.TryGetValue(senderPlayerId, out var sender)) return;
            if (sender.Player.Role != PlayerRole.Guesser) return;

            var team = sender.Player.Team;
            var currentPassword = matchState.GetCurrentPassword(team);
            int currentScore = (team == MatchTeam.RedTeam) ? matchState.RedTeamScore : matchState.BlueTeamScore;

            if (currentPassword != null && (guess.Equals(currentPassword.EnglishWord, StringComparison.OrdinalIgnoreCase) 
                || guess.Equals(currentPassword.SpanishWord, StringComparison.OrdinalIgnoreCase)))
            {
                if (matchState.Status == MatchStatus.SuddenDeath)
                {
                    matchState.Status = MatchStatus.Finished;
                    matchState.RoundTimer?.Dispose();
                    matchState.RoundTimer = null; 

                    if (team == MatchTeam.RedTeam)
                    {
                        matchState.RedTeamScore++;
                    }
                    else 
                    {
                        matchState.BlueTeamScore++;
                    }

                    await PersistAndNotifyGameEnd(matchState, team);
                    return;
                }

                int newScore;
                lock (matchState)
                {
                    if (team == MatchTeam.RedTeam) newScore = ++matchState.RedTeamScore;
                    else newScore = ++matchState.BlueTeamScore;
                }

                var resultDto = new GuessResultDTO { IsCorrect = true, Team = team, NewScore = newScore };
                await BroadcastAsync(matchState, cb => cb.OnGuessResult(resultDto));

                if (team == MatchTeam.RedTeam) matchState.RedTeamWordIndex++;
                else matchState.BlueTeamWordIndex++;

                var nextWord = matchState.GetCurrentPassword(team);
                
                    var clueGuy = GetPlayerByRole(matchState, team, PlayerRole.ClueGuy);
                    var guesser = GetPlayerByRole(matchState, team, PlayerRole.Guesser);

                    if (clueGuy.Callback != null)
                    {
                        try { clueGuy.Callback.OnNewPassword(ToDTO(nextWord)); }
                        catch { await HandlePlayerDisconnectionAsync(matchState, clueGuy.Player.Id); }
                    }
                    if (guesser.Callback != null)
                    {
                        try { guesser.Callback.OnNewPassword(ToDTOForGuesser(nextWord)); }
                        catch { await HandlePlayerDisconnectionAsync(matchState, guesser.Player.Id); }
                    }
                    
            }
            else
            {
                var resultDto = new GuessResultDTO { IsCorrect = false, Team = team, NewScore = currentScore };
                var clueGuy = GetPlayerByRole(matchState, team, PlayerRole.ClueGuy);
                try { sender.Callback.OnGuessResult(resultDto); }
                catch { await HandlePlayerDisconnectionAsync(matchState, sender.Player.Id); }
                try { clueGuy.Callback?.OnGuessResult(resultDto); }
                catch { await HandlePlayerDisconnectionAsync(matchState, clueGuy.Player.Id); }
            }
        }

        public async Task SubmitValidationVotesAsync(string gameCode, int senderPlayerId, List<ValidationVoteDTO> votes)
        {
            try
            {
                log.InfoFormat("SubmitValidationVotesAsync called - GameCode: {0}, PlayerId: {1}, VotesCount: {2}", gameCode, senderPlayerId, votes == null ? 0 : votes.Count);
                if (votes == null)
                {
                    log.WarnFormat("Votes list is null for game '{0}', player {1}", gameCode, senderPlayerId);
                    return;
                }
                if (!matches.TryGetValue(gameCode, out MatchState matchState) || matchState.Status != MatchStatus.Validating)
                {
                    log.WarnFormat("Match not found or not in validating state: {0}", gameCode);
                    return;
                }

                if (!matchState.ActivePlayers.TryGetValue(senderPlayerId, out var sender))
                {
                    log.WarnFormat("Player not found in match: {0}", senderPlayerId);
                    return;
                }

                bool allVotesIn = false;
                lock (matchState.ReceivedVotes)
                {
                    if (matchState.PlayersWhoVoted.Contains(senderPlayerId)) return;

                    matchState.PlayersWhoVoted.Add(senderPlayerId);
                    matchState.ReceivedVotes.Add((sender.Player.Team, votes));

                    if (matchState.PlayersWhoVoted.Count >= 4)
                    {
                        allVotesIn = true;
                    }
                }

                if (allVotesIn)
                {
                    matchState.ValidationTimer?.Dispose();
                    matchState.ValidationTimer = null;
                    await Task.Run(async () => await ProcessVotesAsync(matchState));
                }
                log.InfoFormat("Votes processing scheduled or stored successfully for game '{0}'", gameCode);
            }
            catch (Exception ex)
            {
                var msg = string.Format("Error in SubmitValidationVotesAsync: {0}", ex.Message);
                log.Error(msg, ex);
                log.DebugFormat("Stack Trace: {0}", ex.StackTrace);
                throw;
            }
            
        }

        public async Task SubscribeToMatchAsync(string gameCode, int playerId)
        {
            if (!matches.TryGetValue(gameCode, out MatchState matchState))
            {
                log.WarnFormat("SubscribeToMatchAsync failed - game '{0}' not found or already ended.", gameCode);
                throw FaultExceptionFactory.Create(ServiceErrorCode.SubscriptionError, "MATCH_NOT_FOUND_OR_ENDED", "The match does not exist or has already ended.");
            }
            if (matchState.Status != MatchStatus.WaitingForPlayers)
            {
                log.WarnFormat("SubscribeToMatchAsync failed - game '{0}' already started or finishing.", gameCode);
                throw FaultExceptionFactory.Create(ServiceErrorCode.SubscriptionError, "MATCH_ALREADY_STARTED_OR_FINISHING", "The match has already started or is finishing.");
            }
            var expectedPlayer = matchState.ExpectedPlayers.FirstOrDefault(p => p.Id == playerId);
            if (expectedPlayer == null)
            {
                log.WarnFormat("SubscribeToMatchAsync unauthorized join attempt for player {0} in game '{1}'.", playerId, gameCode);
                throw FaultExceptionFactory.Create(ServiceErrorCode.SubscriptionError, "NOT_AUTHORIZED_TO_JOIN", "You are not authorized to join this match.");
            }
            if (matchState.ActivePlayers.ContainsKey(playerId))
            {
                log.WarnFormat("SubscribeToMatchAsync duplicate join for player {0} in game '{1}'.", playerId, gameCode);
                throw FaultExceptionFactory.Create(ServiceErrorCode.AlreadyInRoom, "PLAYER_ALREADY_IN_MATCH", "Player is already in the match.");
            }
            var callback = operationContext.GetCallbackChannel<IGameManagerCallback>();
            if (!matchState.ActivePlayers.TryAdd(playerId, (callback, expectedPlayer)))
            {
                log.ErrorFormat("SubscribeToMatchAsync internal error adding player {0} to game '{1}'.", playerId, gameCode);
                throw FaultExceptionFactory.Create(ServiceErrorCode.SubscriptionError, "JOIN_INTERNAL_ERROR", "Internal error while joining the match.");
            }
            if (matchState.ActivePlayers.Count == matchState.ExpectedPlayers.Count) await StartGameInternalAsync(matchState);
        }

        private async Task StartGameInternalAsync(MatchState matchState)
        {
            try
            {
                var initState = new MatchInitStateDTO { Players = matchState.ActivePlayers.Values.Select(p => p.Player).ToList() };
                await BroadcastAsync(matchState, callback => callback.OnMatchInitialized(initState));
                await StartNewRoundAsync(matchState);
            }
            catch (Exception ex)
            {                
                log.ErrorFormat("Error starting match {0} \n {1}", matchState.GameCode, ex);
                await BroadcastAsync(matchState, callback => callback.OnMatchCancelled("Error starting the match."));
                matches.TryRemove(matchState.GameCode, out _);
            }
        }

        private async Task StartNewRoundAsync(MatchState matchState)
        {
            matchState.Status = MatchStatus.InProgress;
            matchState.CurrentRound++;

            if (matchState.CurrentRound > 1)
            {
                foreach (var playerEntry in matchState.ActivePlayers.Values)
                {
                    playerEntry.Player.Role = (playerEntry.Player.Role == PlayerRole.ClueGuy) ? PlayerRole.Guesser : PlayerRole.ClueGuy;
                }
            }

            var roundStartState = new RoundStartStateDTO
            {
                CurrentRound = matchState.CurrentRound,
                PlayersWithNewRoles = matchState.ActivePlayers.Values.Select(p => p.Player).ToList()
            };
            await BroadcastAsync(matchState, cb => cb.OnNewRoundStarted(roundStartState));

            matchState.RedTeamWords = await wordRepository.GetRandomWordsAsync(WordsPerRound);
            matchState.BlueTeamWords = await wordRepository.GetRandomWordsAsync(WordsPerRound);
            matchState.RedTeamWordIndex = 0;
            matchState.BlueTeamWordIndex = 0;
            matchState.RedTeamTurnHistory.Clear();
            matchState.BlueTeamTurnHistory.Clear();
            matchState.RedTeamPassedThisRound = false;
            matchState.BlueTeamPassedThisRound = false;

            if (matchState.RedTeamWords.Count < WordsPerRound || matchState.BlueTeamWords.Count < WordsPerRound)
            {
                await BroadcastAsync(matchState, cb => cb.OnMatchCancelled("Error: Not enough words found in the database."));
                matches.TryRemove(matchState.GameCode, out _);
                return;
            }

            matchState.SecondsLeft = RoundDurationSeconds;
            matchState.RoundTimer = new Timer(TimerTickCallback, matchState, 1000, 1000);

            var redClueGuy = GetPlayerByRole(matchState, MatchTeam.RedTeam, PlayerRole.ClueGuy);
            var redGuesser = GetPlayerByRole(matchState, MatchTeam.RedTeam, PlayerRole.Guesser);
            var blueClueGuy = GetPlayerByRole(matchState, MatchTeam.BlueTeam, PlayerRole.ClueGuy);
            var blueGuesser = GetPlayerByRole(matchState, MatchTeam.BlueTeam, PlayerRole.Guesser);

            var redWord = matchState.GetCurrentPassword(MatchTeam.RedTeam);
            if (redClueGuy.Callback != null)
            {
                try { redClueGuy.Callback.OnNewPassword(ToDTO(redWord)); }
                catch { await HandlePlayerDisconnectionAsync(matchState, redClueGuy.Player.Id); }
            }
            if (redGuesser.Callback != null)
            {
                try { redGuesser.Callback.OnNewPassword(ToDTOForGuesser(redWord)); } 
                catch { await HandlePlayerDisconnectionAsync(matchState, redGuesser.Player.Id); }
            }

            var blueWord = matchState.GetCurrentPassword(MatchTeam.BlueTeam);
            if (blueClueGuy.Callback != null)
            {
                try { blueClueGuy.Callback.OnNewPassword(ToDTO(blueWord)); }
                catch { await HandlePlayerDisconnectionAsync(matchState, blueClueGuy.Player.Id); }
            }
            if (blueGuesser.Callback != null)
            {
                try { blueGuesser.Callback.OnNewPassword(ToDTOForGuesser(blueWord)); }
                catch { await HandlePlayerDisconnectionAsync(matchState, blueGuesser.Player.Id); }
            }
        }

        private async void TimerTickCallback(object state)
        {
            var matchState = (MatchState)state;
            if (matchState.Status != MatchStatus.InProgress && matchState.Status != MatchStatus.SuddenDeath) return;
            int newTime = Interlocked.Decrement(ref matchState.SecondsLeft);
            await BroadcastAsync(matchState, cb => cb.OnTimerTick(newTime));
            if (newTime <= 0)
            {
                matchState.RoundTimer?.Dispose();
                matchState.RoundTimer = null;
                await StartValidationPhaseAsync(matchState);
            }
        }

        private async Task StartValidationPhaseAsync(MatchState matchState)
        {
            matchState.Status = MatchStatus.Validating;
            matchState.ReceivedVotes.Clear();
            matchState.PlayersWhoVoted.Clear();

            var redTeamPlayers = GetPlayersByTeam(matchState, MatchTeam.RedTeam);
            var blueTeamPlayers = GetPlayersByTeam(matchState, MatchTeam.BlueTeam);

            if (matchState.RedTeamTurnHistory.Count > 0) await BroadcastToPlayersAsync(blueTeamPlayers, cb => cb.OnBeginRoundValidation(matchState.RedTeamTurnHistory));
            if (matchState.BlueTeamTurnHistory.Count > 0) await BroadcastToPlayersAsync(redTeamPlayers, cb => cb.OnBeginRoundValidation(matchState.BlueTeamTurnHistory));

            if (matchState.RedTeamTurnHistory.Count == 0 && matchState.BlueTeamTurnHistory.Count == 0)
            {
                await ProcessVotesAsync(matchState);
                return;
            }

            matchState.ValidationSecondsLeft = ValidationDurationSeconds;
            matchState.ValidationTimer = new Timer(ValidationTimerTickCallback, matchState, 1000, 1000);
        }
        private async void ValidationTimerTickCallback(object state)
        {
            var matchState = (MatchState)state;
            if (matchState.Status != MatchStatus.Validating) return;

            int newTime = Interlocked.Decrement(ref matchState.ValidationSecondsLeft);
            await BroadcastAsync(matchState, cb => cb.OnValidationTimerTick(newTime));

            if (newTime <= 0)
            {
                matchState.ValidationTimer?.Dispose();
                matchState.ValidationTimer = null;
                await ProcessVotesAsync(matchState);
            }
        }

        private async Task ProcessVotesAsync(MatchState matchState)
        {
            try
            {
                log.InfoFormat("Starting ProcessVotesAsync for game '{0}'", matchState.GameCode);
                matchState.ValidationTimer?.Dispose();
                matchState.ValidationTimer = null;

                int redTeamPenalty = 0;
                int blueTeamPenalty = 0;

                var redTurnsToPenalizeMultiword = new HashSet<int>();
                var blueTurnsToPenalizeMultiword = new HashSet<int>();
                var redTurnsToPenalizeSynonym = new HashSet<int>();
                var blueTurnsToPenalizeSynonym = new HashSet<int>();

                foreach (var (voterTeam, voteList) in matchState.ReceivedVotes)
                {
                    foreach (var vote in voteList)
                    {
                        if (voterTeam == MatchTeam.RedTeam)
                        {
                            if (vote.PenalizeMultiword) blueTurnsToPenalizeMultiword.Add(vote.TurnId);
                            if (vote.PenalizeSynonym) blueTurnsToPenalizeSynonym.Add(vote.TurnId);
                        }
                        else
                        {
                            if (vote.PenalizeMultiword) redTurnsToPenalizeMultiword.Add(vote.TurnId);
                            if (vote.PenalizeSynonym) redTurnsToPenalizeSynonym.Add(vote.TurnId);
                        }
                    }
                }

                redTeamPenalty = (redTurnsToPenalizeMultiword.Count * PenaltyMultiword) + (redTurnsToPenalizeSynonym.Count * PenaltySynonim);
                blueTeamPenalty = (blueTurnsToPenalizeMultiword.Count * PenaltyMultiword) + (blueTurnsToPenalizeSynonym.Count * PenaltySynonim);
                lock (matchState)
                {
                    matchState.RedTeamScore = Math.Max(0, matchState.RedTeamScore - redTeamPenalty);
                    matchState.BlueTeamScore = Math.Max(0, matchState.BlueTeamScore - blueTeamPenalty);
                }
                var validationResult = new ValidationResultDTO
                {
                    TotalPenaltyApplied = redTeamPenalty + blueTeamPenalty,
                    NewRedTeamScore = matchState.RedTeamScore,
                    NewBlueTeamScore = matchState.BlueTeamScore,
                };
                log.InfoFormat("Before BroadcastAsync - sending ValidationComplete for game '{0}'", matchState.GameCode);
                await BroadcastAsync(matchState, cb => cb.OnValidationComplete(validationResult));
                log.Info("After BroadcastAsync - all callbacks completed");
                if (matchState.CurrentRound >= TotalRounds)
                {
                    await EndGameAsync(matchState);
                }
                else
                {
                    await StartNewRoundAsync(matchState);
                }
                log.InfoFormat("ProcessVotesAsync completed successfully for game '{0}'", matchState.GameCode);

            }
            catch (Exception ex)
            {
                var msg = string.Format("Error in ProcessVotesAsync: {0}", ex.Message);
                log.Error(msg, ex);
                throw;
            }
        }

        private async Task HandlePlayerDisconnectionAsync(MatchState matchState, int disconnectedPlayerId)
        {
            if (matchState.Status == MatchStatus.Finished) return;
            if (matchState.ActivePlayers.TryRemove(disconnectedPlayerId, out var disconnectedPlayer))
            {
                matchState.Status = MatchStatus.Finished;
                matchState.RoundTimer?.Dispose(); // Stop timers
                matchState.ValidationTimer?.Dispose();
                matchState.RoundTimer = null;
                matchState.ValidationTimer = null;
                await BroadcastAsync(matchState, cb => cb.OnMatchCancelled(string.Format("Player {0} has disconnected.", disconnectedPlayer.Player.Nickname)));
                matches.TryRemove(matchState.GameCode, out _);
            }
        }

        private async Task EndGameAsync(MatchState matchState)
        {
            if (matchState.RedTeamScore == matchState.BlueTeamScore)
            {
                await StartSuddenDeathAsync(matchState);
                return; 
            }
            matchState.Status = MatchStatus.Finished;
            MatchTeam? winner = (matchState.RedTeamScore > matchState.BlueTeamScore) ? MatchTeam.RedTeam : MatchTeam.BlueTeam;
            await PersistAndNotifyGameEnd(matchState, winner);
        }
        
        private async Task StartSuddenDeathAsync(MatchState matchState)
        {
            matchState.Status = MatchStatus.SuddenDeath;

            await BroadcastAsync(matchState, cb => cb.OnSuddenDeathStarted());

            matchState.RedTeamWords = await wordRepository.GetRandomWordsAsync(1);
            matchState.BlueTeamWords = await wordRepository.GetRandomWordsAsync(1);

            if (matchState.RedTeamWords.Count < 1 || matchState.BlueTeamWords.Count < 1)
            {
                await BroadcastAsync(matchState, cb => cb.OnMatchCancelled("Error: Could not retrieve words for sudden death."));
                matches.TryRemove(matchState.GameCode, out _);
                return;
            }

            matchState.RedTeamWordIndex = 0;
            matchState.BlueTeamWordIndex = 0;
            matchState.RedTeamTurnHistory.Clear(); 
            matchState.BlueTeamTurnHistory.Clear();
            matchState.RedTeamPassedThisRound = true;
            matchState.BlueTeamPassedThisRound = true;

            
            matchState.SecondsLeft = SuddenDeathDurationSeconds;
            matchState.RoundTimer = new Timer(TimerTickCallback, matchState, 1000, 1000);

            var redClueGuy = GetPlayerByRole(matchState, MatchTeam.RedTeam, PlayerRole.ClueGuy);
            var blueClueGuy = GetPlayerByRole(matchState, MatchTeam.BlueTeam, PlayerRole.ClueGuy);
            if (redClueGuy.Callback != null)
            {
                try { redClueGuy.Callback.OnNewPassword(ToDTO(matchState.GetCurrentPassword(MatchTeam.RedTeam))); }
                catch { await HandlePlayerDisconnectionAsync(matchState, redClueGuy.Player.Id); }
            }
            if (blueClueGuy.Callback != null)
            {
                try { blueClueGuy.Callback.OnNewPassword(ToDTO(matchState.GetCurrentPassword(MatchTeam.BlueTeam))); }
                catch { await HandlePlayerDisconnectionAsync(matchState, blueClueGuy.Player.Id); }
            }
        }
        private async Task PersistAndNotifyGameEnd(MatchState matchState, MatchTeam? winner)
        {
            var redTeamPlayers = GetPlayersByTeam(matchState, MatchTeam.RedTeam).Select(p => p.Player);
            var blueTeamPlayers = GetPlayersByTeam(matchState, MatchTeam.BlueTeam).Select(p => p.Player);

            var registeredRedPlayerIds = redTeamPlayers.Where(p => p.Id > 0).Select(p => p.Id).ToList();

            var registeredBluePlayerIds = blueTeamPlayers.Where(p => p.Id > 0).Select(p => p.Id).ToList();

            try
            {
                await matchRepository.SaveMatchResultAsync(matchState.RedTeamScore, matchState.BlueTeamScore,
                    registeredRedPlayerIds, registeredBluePlayerIds);
                if (winner.HasValue)
                {
                    var winningPlayerIds = (winner == MatchTeam.RedTeam) ? registeredRedPlayerIds : registeredBluePlayerIds;
                    if (winningPlayerIds.Any())
                    {
                        await Task.WhenAll(winningPlayerIds.Select(id => playerRepository.UpdatePlayerTotalPointsAsync(id, PointsPerWin)));
                    }
                }
            }
            catch (Exception ex) 
            {
                log.ErrorFormat("ERROR persisting match {0}: {1}", matchState.GameCode, ex.Message); 
            }
            var summary = new MatchSummaryDTO { WinnerTeam = winner, RedScore = matchState.RedTeamScore, BlueScore = matchState.BlueTeamScore };
            await BroadcastAsync(matchState, cb => cb.OnMatchOver(summary));
            if (matches.TryRemove(matchState.GameCode, out var removedMatch)) 
            {
                removedMatch.Dispose();
            }
        }

        private static async Task BroadcastAsync(MatchState game, Action<IGameManagerCallback> action)
        {
            try
            {
                var disconnectedPlayers = new ConcurrentBag<int>();
                var tasks = game.ActivePlayers.Select(async playerEntry =>
                {
                    try
                    {
                        // Execute the callback synchronously within the task
                        action(playerEntry.Value.Callback);
                        await Task.CompletedTask; // To keep the method async
                    }
                    catch (CommunicationException ex)
                    {
                        log.WarnFormat("CommunicationException in callback for player {0}: {1}", playerEntry.Key, ex.Message);
                        disconnectedPlayers.Add(playerEntry.Key);
                    }
                    catch (TimeoutException ex)
                    {
                        log.WarnFormat("TimeoutException in callback for player {0}: {1}", playerEntry.Key, ex.Message);
                        disconnectedPlayers.Add(playerEntry.Key);
                    }
                    catch (Exception ex)
                    {
                        var msg = string.Format("Unexpected error in callback for player {0}: {1}", playerEntry.Key, ex.Message);
                        log.Error(msg, ex);
                        disconnectedPlayers.Add(playerEntry.Key);
                    }
                });

                await Task.WhenAll(tasks);
                foreach (var playerId in disconnectedPlayers)
                {
                    if (game.ActivePlayers.TryRemove(playerId, out _))
                    {
                        log.InfoFormat("Successfully removed disconnected player: {0}", playerId);
                    }
                }
            }
            catch (Exception ex)
            {
                var msg = string.Format("Error in BroadcastAsync: {0}", ex.Message);
                log.Error(msg, ex);
                throw;
            }
        }

        private static async Task BroadcastToPlayersAsync(IEnumerable<(IGameManagerCallback Callback, PlayerDTO Player)> players, Action<IGameManagerCallback> action)
        {
            var tasks = players.Select(playerEntry => Task.Run(() => {
                try 
                { 
                    action(playerEntry.Callback); 
                } 
                catch 
                { 
                    // Ignore disconnection here
                }
            }));
            await Task.WhenAll(tasks);
        }

        private PasswordWordDTO ToDTO(PasswordWord entity)
        {
            if (entity == null) return new PasswordWordDTO { SpanishWord = "END", EnglishWord = "END" };
            return new PasswordWordDTO { EnglishWord = entity.EnglishWord, SpanishWord = entity.SpanishWord, EnglishDescription = entity.EnglishDescription, SpanishDescription = entity.SpanishDescription };
        }
        private PasswordWordDTO ToDTOForGuesser(PasswordWord entity)
        {
            if (entity == null) return new PasswordWordDTO { SpanishWord = "END", EnglishWord = "END" };

            return new PasswordWordDTO
            {
                EnglishWord = string.Empty, 
                SpanishWord = string.Empty,
                EnglishDescription = entity.EnglishDescription,
                SpanishDescription = entity.SpanishDescription
            };
        }

        private static IEnumerable<(IGameManagerCallback Callback, PlayerDTO Player)> GetPlayersByTeam(MatchState state, MatchTeam team) => state.ActivePlayers.Values.Where(p => p.Player.Team == team);
        private static (IGameManagerCallback Callback, PlayerDTO Player) GetPlayerByRole(MatchState state, MatchTeam team, PlayerRole role) => state.ActivePlayers.Values.FirstOrDefault(p => p.Player.Team == team && p.Player.Role == role);
        private static (IGameManagerCallback Callback, PlayerDTO Player) GetPartner(MatchState state, (IGameManagerCallback Callback, PlayerDTO Player) player) => state.ActivePlayers.Values.FirstOrDefault(p => p.Player.Team == player.Player.Team && p.Player.Id != player.Player.Id);
    }
}