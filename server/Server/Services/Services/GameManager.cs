using Data.DAL.Interfaces;
using Data.Model;
using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Services.Wrappers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
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

            // Players expected to join this match (from waiting room)
            public List<PlayerDTO> ExpectedPlayers { get; }

            // Players currently connected to the match
            public ConcurrentDictionary<int, (IGameManagerCallback Callback, PlayerDTO Player)> ActivePlayers { get; }

            public int RedTeamScore { get; set; }
            public int BlueTeamScore { get; set; }
            public Timer RoundTimer { get; set; }
            public int SecondsLeft; // Managed with Interlocked
            public int CurrentRound { get; set; }
            public MatchTeam CurrentTurnTeam { get; set; } // Team currently taking the turn
            public List<PasswordWord> CurrentRoundWords { get; set; } // The5 words for the round
            public int CurrentWordIndex { get; set; }
            public List<TurnHistoryDTO> CurrentTurnHistory { get; set; }
            public List<List<ValidationVoteDTO>> ReceivedVotes { get; }
            public HashSet<int> PlayersWhoVoted { get; }

            public MatchState(string gameCode, List<PlayerDTO> expectedPlayers)
            {
                GameCode = gameCode;
                ExpectedPlayers = expectedPlayers;
                Status = MatchStatus.WaitingForPlayers;
                ActivePlayers = new ConcurrentDictionary<int, (IGameManagerCallback, PlayerDTO)>();
                CurrentRoundWords = new List<PasswordWord>();
                CurrentTurnHistory = new List<TurnHistoryDTO>();
                RedTeamScore = 0;
                BlueTeamScore = 0;
                CurrentRound = 1;
                CurrentTurnTeam = MatchTeam.RedTeam;
                ReceivedVotes = new List<List<ValidationVoteDTO>>();
                PlayersWhoVoted = new HashSet<int>();
            }
            public PasswordWord GetCurrentPassword()
            {
                if (CurrentWordIndex < CurrentRoundWords.Count)
                    return CurrentRoundWords[CurrentWordIndex];
                return null;
            }
            public void Dispose()
            {
                RoundTimer?.Dispose();
            }
        }

        private readonly ConcurrentDictionary<string, MatchState> matches = new ConcurrentDictionary<string, MatchState>();
        private readonly IOperationContextWrapper operationContext;
        private readonly IWordRepository wordRepository;
        private readonly IMatchRepository matchRepository;
        private readonly IPlayerRepository playerRepository;
        private readonly ILog log = LogManager.GetLogger(typeof(GameManager));
        private const int ROUND_DURATION_SECONDS = 60;
        private const int WORDS_PER_ROUND = 5;
        private const int TOTAL_ROUNDS = 5;
        private const int POINTS_PER_WIN = 10;
        private const int PENALTY_SYNONYM = 2;
        private const int PENALTY_MULTIWORD = 1;

        public GameManager(IOperationContextWrapper contextWrapper, IWordRepository wordRepository, IMatchRepository matchRepository,
            IPlayerRepository playerRepository)
        {
            this.operationContext = contextWrapper;
            this.wordRepository = wordRepository;
            this.matchRepository = matchRepository;
            this.playerRepository = playerRepository;
        }

        // ------------------------
        // Public API (top)
        // ------------------------

        public bool CreateMatch(string gameCode, List<PlayerDTO> playersFromWaitingRoom)
        {
            if (playersFromWaitingRoom == null || playersFromWaitingRoom.Count != 4)
            {
                // Must have exactly4 players
                return false;
            }

            var matchState = new MatchState(gameCode, playersFromWaitingRoom);
            return matches.TryAdd(gameCode, matchState);
        }

        public Task PassTurnAsync(string gameCode, int senderPlayerId)
        {
            // Placeholder: not implemented yet
            return Task.CompletedTask;
        }

        public async Task SubmitClueAsync(string gameCode, int senderPlayerId, string clue)
        {
            if (!matches.TryGetValue(gameCode, out var matchState) || matchState.Status != MatchStatus.InProgress)
                return;

            if (!matchState.ActivePlayers.TryGetValue(senderPlayerId, out var sender))
                return; // Player does not exist

            // Validate role and current turn
            if (sender.Player.Role != PlayerRole.ClueGuy || sender.Player.Team != matchState.CurrentTurnTeam)
            {
                // Optional: throw FaultException
                return;
            }

            // Save the clue for later validation
            var currentPassword = matchState.GetCurrentPassword();
            matchState.CurrentTurnHistory.Add(new TurnHistoryDTO
            {
                TurnId = matchState.CurrentWordIndex,
                Password = ToDTO(currentPassword),
                ClueUsed = clue
            });

            // Send the clue to the teammate (partner)
            var partner = GetPartner(matchState, sender);
            if (partner.Callback != null)
            {
                try
                {
                    partner.Callback.OnClueReceived(clue);
                }
                catch { await HandlePlayerDisconnectionAsync(matchState, partner.Player.Id); }
            }
        }

        public async Task SubmitGuessAsync(string gameCode, int senderPlayerId, string guess)
        {
            if (!matches.TryGetValue(gameCode, out var matchState) || matchState.Status != MatchStatus.InProgress)
                return;

            if (!matchState.ActivePlayers.TryGetValue(senderPlayerId, out var sender))
                return; // Player does not exist

            // Validate role and current turn
            if (sender.Player.Role != PlayerRole.Guesser || sender.Player.Team != matchState.CurrentTurnTeam)
            {
                // Optional: throw FaultException
                return;
            }

            var team = sender.Player.Team;
            var currentPassword = matchState.GetCurrentPassword();
            int currentScore = (team == MatchTeam.RedTeam) ? matchState.RedTeamScore : matchState.BlueTeamScore;

            // Compare the guess
            if (currentPassword != null && (guess.Equals(currentPassword.EnglishWord, StringComparison.OrdinalIgnoreCase))
 || guess.Equals(currentPassword.SpanishWord, StringComparison.OrdinalIgnoreCase))
            {
                // --- SUCCESS CASE ---
                int newScore;
                lock (matchState)
                {
                    if (team == MatchTeam.RedTeam)
                    {
                        matchState.RedTeamScore++;
                        newScore = matchState.RedTeamScore;
                    }
                    else
                    {
                        matchState.BlueTeamScore++;
                        newScore = matchState.BlueTeamScore;
                    }
                }

                var resultDto = new GuessResultDTO { IsCorrect = true, Team = team, NewScore = newScore };
                await BroadcastAsync(matchState, cb => cb.OnGuessResult(resultDto)); // Notify everyone

                matchState.CurrentWordIndex++; // Move to the next word

                if (matchState.CurrentWordIndex >= WORDS_PER_ROUND)
                {
                    // The5 words are done. End the round and start validation
                    matchState.RoundTimer?.Dispose();
                    matchState.RoundTimer = null;
                    await StartValidationPhaseAsync(matchState);
                }
                else
                {
                    // Send the next word to the active team's ClueGuy
                    var pistero = GetPlayerByRole(matchState, team, PlayerRole.ClueGuy);
                    if (pistero.Callback != null)
                    {
                        try
                        {
                            var nextWord = matchState.GetCurrentPassword();
                            pistero.Callback.OnNewPassword(ToDTO(nextWord));
                        }
                        catch { await HandlePlayerDisconnectionAsync(matchState, pistero.Player.Id); }
                    }
                }
            }
            else
            {
                // --- FAILURE CASE ---
                var resultDto = new GuessResultDTO { IsCorrect = false, Team = team, NewScore = currentScore };

                // Notify only the active team
                var pistero = GetPlayerByRole(matchState, team, PlayerRole.ClueGuy);
                try
                {
                    sender.Callback.OnGuessResult(resultDto);
                } // Guesser
                catch
                {
                    await HandlePlayerDisconnectionAsync(matchState, sender.Player.Id);
                }
                try
                {
                    pistero.Callback?.OnGuessResult(resultDto);
                } // ClueGuy
                catch
                {
                    await HandlePlayerDisconnectionAsync(matchState, pistero.Player.Id);
                }
            }
        }

        public async Task SubmitValidationVotesAsync(string gameCode, int senderPlayerId, List<ValidationVoteDTO> votes)
        {
            if (!matches.TryGetValue(gameCode, out var matchState) || matchState.Status != MatchStatus.Validating)
                return; // Not voting time

            if (!matchState.ActivePlayers.TryGetValue(senderPlayerId, out var sender))
                return; // Player does not exist

            var teamThatPlayed = matchState.CurrentTurnTeam;
            var validatingTeam = (teamThatPlayed == MatchTeam.RedTeam) ? MatchTeam.BlueTeam : MatchTeam.RedTeam;

            if (sender.Player.Team != validatingTeam)
                return; // Wrong team is trying to vote

            // Synchronize multiple votes
            lock (matchState.ReceivedVotes)
            {
                if (matchState.PlayersWhoVoted.Contains(senderPlayerId))
                    return; // Already voted

                matchState.PlayersWhoVoted.Add(senderPlayerId);
                matchState.ReceivedVotes.Add(votes);

                // Check if both validators have voted
                var validators = GetPlayersByTeam(matchState, validatingTeam);
                if (matchState.PlayersWhoVoted.Count < validators.Count())
                {
                    return; // Missing votes
                }

                // All votes received
            }

            // Process votes outside the lock to avoid blocking async work
            await ProcessVotesAsync(matchState, teamThatPlayed);
        }

        public async Task SubscribeToMatchAsync(string gameCode, int playerId)
        {
            if (!matches.TryGetValue(gameCode, out var matchState))
            {
                throw new FaultException("The match does not exist or has already finished.");
            }

            if (matchState.Status != MatchStatus.WaitingForPlayers)
            {
                throw new FaultException("The match has already started or is finishing.");
            }

            // Validate that the player is one of the expected players
            var expectedPlayer = matchState.ExpectedPlayers.FirstOrDefault(p => p.Id == playerId);
            if (expectedPlayer == null)
            {
                log.WarnFormat("Unauthorized join attempt to match {0} by player {1}.", gameCode, playerId);
                
                throw new FaultException("You are not authorized to join this match.");
            }

            if (matchState.ActivePlayers.ContainsKey(playerId))
            {
                var errorDetail = new ServiceErrorDetailDTO
                {
                    Code = ServiceErrorCode.AlreadyInRoom,
                    ErrorCode = "PLAYER_ALREADY_IN_MATCH"
                };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail,new FaultReason(errorDetail.ErrorCode));
            }

            var callback = operationContext.GetCallbackChannel<IGameManagerCallback>();
            if (!matchState.ActivePlayers.TryAdd(playerId, (callback, expectedPlayer)))
            {
                throw new FaultException("Internal error while joining the match.");
            }

            // If this is the last player to join, start the match
            if (matchState.ActivePlayers.Count == matchState.ExpectedPlayers.Count)
            {
                await StartGameInternalAsync(matchState);
            }
        }

        // ------------------------
        // Private helpers (middle)
        // ------------------------

        private async Task StartGameInternalAsync(MatchState matchState)
        {
            try
            {
                matchState.Status = MatchStatus.InProgress;
                matchState.CurrentRound = 1;

                var initState = new MatchInitStateDTO
                {
                    Players = matchState.ActivePlayers.Values.Select(p => p.Player).ToList()
                };

                await BroadcastAsync(matchState, callback => callback.OnMatchInitialized(initState));
                await StartRoundTurnAsync(matchState);
            }
            catch (Exception)
            {
                // If something fails (e.g., no words in the DB), cancel the match
                await BroadcastAsync(matchState, callback => callback.OnMatchCancelled("Error while starting the match."));
                matches.TryRemove(matchState.GameCode, out _);
            }
        }

        private static async Task BroadcastAsync(MatchState game, Action<IGameManagerCallback> action)
        {
            var tasks = game.ActivePlayers.Select(playerEntry =>
        Task.Run(() =>
        {
            try
            {
                action(playerEntry.Value.Callback);
            }
            catch
            {
                // Disconnection handling: remove unreachable player
                game.ActivePlayers.TryRemove(playerEntry.Key, out _);
            }
        })
    );
            await Task.WhenAll(tasks);
        }

        //METODO MODIFICADO METODO ORIGINAL ABAJO
        private async Task StartRoundTurnAsync(MatchState matchState)
        {
            matchState.Status = MatchStatus.InProgress;
            
            if (matchState.CurrentTurnTeam == MatchTeam.RedTeam)
            {
                matchState.CurrentRoundWords = await wordRepository.GetRandomWordsAsync(WORDS_PER_ROUND);
            }

            matchState.CurrentWordIndex = 0;
            matchState.CurrentTurnHistory = new List<TurnHistoryDTO>();

            if (matchState.CurrentRoundWords == null || matchState.CurrentRoundWords.Count == 0)
            {
                await BroadcastAsync(matchState, cb => cb.OnMatchCancelled("Error: No words were found in the database."));
                return;
            }

            matchState.SecondsLeft = ROUND_DURATION_SECONDS;
            matchState.RoundTimer = new Timer(TimerTickCallback, matchState, 1000, 1000);

            // Send the first word to the current team's ClueGuy
            var pistero = GetPlayerByRole(matchState, matchState.CurrentTurnTeam, PlayerRole.ClueGuy);
            if (pistero.Callback != null)
            {
                try
                {
                    var firstWordEntity = matchState.GetCurrentPassword();
                    pistero.Callback.OnNewPassword(ToDTO(firstWordEntity));
                }
                catch
                {
                    await HandlePlayerDisconnectionAsync(matchState, pistero.Player.Id);
                }
            }
        }
        

        private async void TimerTickCallback(object state)
        {
            var matchState = (MatchState)state;
            if (matchState.Status != MatchStatus.InProgress) return;

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
            var teamThatPlayed = matchState.CurrentTurnTeam;
            var validatingTeam = (teamThatPlayed == MatchTeam.RedTeam) ? MatchTeam.BlueTeam : MatchTeam.RedTeam;

            // Clear previous validation data
            matchState.ReceivedVotes.Clear();
            matchState.PlayersWhoVoted.Clear();

            // Gather current turn history
            var historyToValidate = matchState.CurrentTurnHistory;
            if (historyToValidate.Count == 0)
            {
                // Nothing to validate (e.g., time ended without clues)
                // Proceed to process the (empty) votes
                await ProcessVotesAsync(matchState, teamThatPlayed);
                return;
            }

            // Send history to validators
            var validators = GetPlayersByTeam(matchState, validatingTeam);
            await BroadcastToPlayersAsync(validators, cb => cb.OnBeginRoundValidation(historyToValidate));

            // Optional: Start a voting timer to force close the phase
        }

        private async Task HandlePlayerDisconnectionAsync(MatchState matchState, int disconnectedPlayerId)
        {
            if (matchState.Status == MatchStatus.Finished) return;
            // Basic disconnection handling for now
            if (matchState.ActivePlayers.TryRemove(disconnectedPlayerId, out var disconnectedPlayer))
            {
                matchState.Status = MatchStatus.Finished;
                matchState.RoundTimer?.Dispose();
                matchState.RoundTimer = null;
                await BroadcastAsync(matchState, cb => cb.OnMatchCancelled($"Player {disconnectedPlayer.Player.Nickname} has disconnected."));
                matches.TryRemove(matchState.GameCode, out _);
            }
        }

        private async Task ProcessVotesAsync(MatchState matchState, MatchTeam teamThatPlayed)
        {
            var synonymPenaltyTurns = new HashSet<int>();
            var multiwordPenaltyTurns = new HashSet<int>();

            foreach (var voteList in matchState.ReceivedVotes)
            {
                foreach (var vote in voteList)
                {
                    if (vote.PenalizeSynonym) synonymPenaltyTurns.Add(vote.TurnId);
                    if (vote.PenalizeMultiword) multiwordPenaltyTurns.Add(vote.TurnId);
                }
            }

            int synonymPenalty = synonymPenaltyTurns.Count * PENALTY_SYNONYM; //2 points each
            int multiwordPenalty = multiwordPenaltyTurns.Count * PENALTY_MULTIWORD; //1 point each
            int totalPenalty = synonymPenalty + multiwordPenalty;

            lock (matchState)
            {
                if (teamThatPlayed == MatchTeam.RedTeam)
                {
                    matchState.RedTeamScore = Math.Max(0, matchState.RedTeamScore - totalPenalty);
                }
                else
                {
                    matchState.BlueTeamScore = Math.Max(0, matchState.BlueTeamScore - totalPenalty);
                }
            }

            // Notify all players with the validation result
            var validationResult = new ValidationResultDTO
            {
                TeamThatWasValidated = teamThatPlayed,
                TotalPenaltyApplied = totalPenalty,
                NewRedTeamScore = matchState.RedTeamScore,
                NewBlueTeamScore = matchState.BlueTeamScore
            };
            await BroadcastAsync(matchState, cb => cb.OnValidationComplete(validationResult));

            // Continue game flow after validation
            if (teamThatPlayed == MatchTeam.RedTeam)
            {
                // Red just played; switch to Blue team
                matchState.CurrentTurnTeam = MatchTeam.BlueTeam;
                await StartRoundTurnAsync(matchState);
            }
            else
            {
                // Blue just played; the round is complete
                matchState.CurrentRound++;

                if (matchState.CurrentRound > TOTAL_ROUNDS)
                {
                    await EndGameAsync(matchState);
                }
                else
                {
                    // Start next round with Red team
                    matchState.CurrentTurnTeam = MatchTeam.RedTeam;
                    await StartRoundTurnAsync(matchState);
                }
            }
        }

        private static async Task BroadcastToPlayersAsync(IEnumerable<(IGameManagerCallback Callback, PlayerDTO Player)> players, Action<IGameManagerCallback> action)
        {
            // Broadcast to a subset of players (e.g., the validators)
            var tasks = players.Select(playerEntry =>
        Task.Run(() =>
        {
            try { action(playerEntry.Callback); }
            catch { /* Optionally handle disconnection here as well */ }
        })
    );
            await Task.WhenAll(tasks);
        }

        private async Task EndGameAsync(MatchState matchState)
        {
            matchState.Status = MatchStatus.Finished;
            if (matchState.RedTeamScore == matchState.BlueTeamScore)
            {
                // For now, declare a draw in case of tie
                await PersistAndNotifyGameEnd(matchState, null);
            }
            else
            {
                // Determine winner
                var winner = (matchState.RedTeamScore > matchState.BlueTeamScore)
                ? MatchTeam.RedTeam
                : MatchTeam.BlueTeam;
                await PersistAndNotifyGameEnd(matchState, winner);
            }
        }

        private async Task PersistAndNotifyGameEnd(MatchState matchState, MatchTeam? winner)
        {
            var redTeamPlayers = GetPlayersByTeam(matchState, MatchTeam.RedTeam).Select(p => p.Player);
            var blueTeamPlayers = GetPlayersByTeam(matchState, MatchTeam.BlueTeam).Select(p => p.Player);

            // Persistence logic (IDs only)
            var redTeamPlayerIds = redTeamPlayers.Where(p => p.Id > 0).Select(p => p.Id);
            var blueTeamPlayerIds = blueTeamPlayers.Where(p => p.Id > 0).Select(p => p.Id);

            try
            {
                await matchRepository.SaveMatchResultAsync(
                matchState.RedTeamScore,
                matchState.BlueTeamScore,
                redTeamPlayerIds,
                blueTeamPlayerIds);

                if (winner.HasValue)
                {
                    var winningPlayerIds = (winner == MatchTeam.RedTeam) ? redTeamPlayerIds : blueTeamPlayerIds;
                    var updateTasks = winningPlayerIds.Select(id => playerRepository.UpdatePlayerTotalPointsAsync(id, POINTS_PER_WIN));
                    await Task.WhenAll(updateTasks);
                }
            }
            catch (Exception ex)
            {
                // Log persist errors (do not block notifying clients)
                Console.WriteLine($"ERROR persisting match: {ex.Message}");
            }

            var summary = new MatchSummaryDTO
            {
                WinnerTeam = winner,
                RedScore = matchState.RedTeamScore,
                BlueScore = matchState.BlueTeamScore
            };

            // Notify all clients
            await BroadcastAsync(matchState, cb => cb.OnMatchOver(summary));

            // Remove match from memory
            if (matches.TryRemove(matchState.GameCode, out var removedMatch))
            {
                removedMatch.Dispose();
            }
        }

        private PasswordWordDTO ToDTO(PasswordWord entity)
        {
            if (entity == null) return new PasswordWordDTO();

            return new PasswordWordDTO
            {
                EnglishWord = entity.EnglishWord,
                SpanishWord = entity.SpanishWord,
                EnglishDescription = entity.EnglishDescription,
                SpanishDescription = entity.SpanishDescription
            };
        }

        // ------------------------
        // Tuple-returning helpers 
        // ------------------------

        private static IEnumerable<(IGameManagerCallback Callback, PlayerDTO Player)> GetPlayersByTeam(MatchState state, MatchTeam team)
        {
            return state.ActivePlayers.Values.Where(p => p.Player.Team == team);
        }

        private static (IGameManagerCallback Callback, PlayerDTO Player) GetPlayerByRole(MatchState state, MatchTeam team, PlayerRole role)
        {
            return state.ActivePlayers.Values.FirstOrDefault(p => p.Player.Team == team && p.Player.Role == role);
        }

        private static (IGameManagerCallback Callback, PlayerDTO Player) GetPartner(MatchState state, (IGameManagerCallback Callback, PlayerDTO Player) player)
        {
            // Find the teammate (same team, different player)
            return state.ActivePlayers.Values.FirstOrDefault(p => p.Player.Team == player.Player.Team && p.Player.Id != player.Player.Id);
        }
    }
}
