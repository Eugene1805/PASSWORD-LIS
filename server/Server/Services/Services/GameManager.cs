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
            public Timer ValidationTimer { get; set; } // ADDED: Timer for the validation phase.
            public int SecondsLeft;
            public int ValidationSecondsLeft; // ADDED: Seconds left for validation.
            public int CurrentRound { get; set; }

            // --- Logic for Simultaneous Gameplay ---
            public List<PasswordWord> RedTeamWords { get; set; }
            public List<PasswordWord> BlueTeamWords { get; set; }
            public int RedTeamWordIndex { get; set; }
            public int BlueTeamWordIndex { get; set; }
            public List<TurnHistoryDTO> RedTeamTurnHistory { get; set; }
            public List<TurnHistoryDTO> BlueTeamTurnHistory { get; set; }
            public bool RedTeamPassedThisRound { get; set; } // ADDED: Tracks if the red team used their pass.
            public bool BlueTeamPassedThisRound { get; set; } // ADDED: Tracks if the blue team used their pass.
            // -------------------------------------

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
                ValidationTimer?.Dispose(); // ADDED: Ensure validation timer is disposed.
            }
        }

        private readonly ConcurrentDictionary<string, MatchState> matches = new ConcurrentDictionary<string, MatchState>();
        private readonly IOperationContextWrapper operationContext;
        private readonly IWordRepository wordRepository;
        private readonly IMatchRepository matchRepository;
        private readonly IPlayerRepository playerRepository;
        private readonly ILog log = LogManager.GetLogger(typeof(GameManager));
        private const int ROUND_DURATION_SECONDS = 180; // CAMBIADO PARA PRUEBAS de 60 a 180
        private const int VALIDATION_DURATION_SECONDS = 60; //Cambiado para pruebas de 20 a 60
        private const int SUDDEN_DEATH_DURATION_SECONDS = 30;
        private const int WORDS_PER_ROUND = 5;
        private const int TOTAL_ROUNDS = 5;
        private const int POINTS_PER_WIN = 10;
        private const int PENALTY_SYNONYM = 2;
        private const int PENALTY_MULTIWORD = 1;

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
            // --- IMPLEMENTED ---
            if (!matches.TryGetValue(gameCode, out MatchState matchState) || matchState.Status != MatchStatus.InProgress) return;
            if (!matchState.ActivePlayers.TryGetValue(senderPlayerId, out var sender)) return;
            if (sender.Player.Role != PlayerRole.ClueGuy) return;

            var team = sender.Player.Team;
            if ((team == MatchTeam.RedTeam && matchState.RedTeamPassedThisRound) ||
                (team == MatchTeam.BlueTeam && matchState.BlueTeamPassedThisRound))
            {
                // Player's team has already passed this round, ignore request.
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

            // Enviar al Adivinador (partner)
            var partner = GetPartner(matchState, sender);
            if (partner.Callback != null)
            {
                // ¡Importante! Enviar la nueva palabra Y la notificación de "Pass"
                try { partner.Callback.OnNewPassword(ToDTOForGuesser(nextWord)); } // <-- AÑADIR ESTO
                catch { await HandlePlayerDisconnectionAsync(matchState, partner.Player.Id); }

                try { partner.Callback.OnClueReceived("Your partner passed the word."); }
                catch { await HandlePlayerDisconnectionAsync(matchState, partner.Player.Id); }
            }
        }


        public async Task SubmitClueAsync(string gameCode, int senderPlayerId, string clue)
        {
            // ADDED: Validation for empty or whitespace clues.
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
            // ADDED: Validation for empty or whitespace guesses.
            if (string.IsNullOrWhiteSpace(guess)) return;

            if (!matches.TryGetValue(gameCode, out MatchState matchState) || matchState.Status != MatchStatus.InProgress) return;
            if (!matchState.ActivePlayers.TryGetValue(senderPlayerId, out var sender)) return;
            if (sender.Player.Role != PlayerRole.Guesser) return;

            var team = sender.Player.Team;
            var currentPassword = matchState.GetCurrentPassword(team);
            int currentScore = (team == MatchTeam.RedTeam) ? matchState.RedTeamScore : matchState.BlueTeamScore;

            if (currentPassword != null && (guess.Equals(currentPassword.EnglishWord, StringComparison.OrdinalIgnoreCase) || guess.Equals(currentPassword.SpanishWord, StringComparison.OrdinalIgnoreCase)))
            {
                if (matchState.Status == MatchStatus.SuddenDeath)
                {
                    // We have an instant winner!
                    matchState.Status = MatchStatus.Finished;
                    matchState.RoundTimer?.Dispose();
                    matchState.RoundTimer = null;

                    // Add the final winning point to the score.
                    if (team == MatchTeam.RedTeam) matchState.RedTeamScore++;
                    else matchState.BlueTeamScore++;

                    // End the game immediately, declaring the guessing team as the winner.
                    await PersistAndNotifyGameEnd(matchState, team);
                    return; // IMPORTANT: Stop execution here.
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
                if (nextWord != null)
                {
                    var clueGuy = GetPlayerByRole(matchState, team, PlayerRole.ClueGuy);
                    var guesser = GetPlayerByRole(matchState, team, PlayerRole.Guesser); // <-- Obtener Adivinador

                    if (clueGuy.Callback != null)
                    {
                        try { clueGuy.Callback.OnNewPassword(ToDTO(nextWord)); }
                        catch { await HandlePlayerDisconnectionAsync(matchState, clueGuy.Player.Id); }
                    }
                    if (guesser.Callback != null) // <-- Añadir envío al Adivinador
                    {
                        try { guesser.Callback.OnNewPassword(ToDTOForGuesser(nextWord)); }
                        catch { await HandlePlayerDisconnectionAsync(matchState, guesser.Player.Id); }
                    }
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
            // ADDED: Validation for null votes list.
            if (votes == null) return;

            if (!matches.TryGetValue(gameCode, out MatchState matchState) || matchState.Status != MatchStatus.Validating) return;
            if (!matchState.ActivePlayers.TryGetValue(senderPlayerId, out var sender)) return;

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
                matchState.ValidationTimer?.Dispose(); // Stop the timer early.
                matchState.ValidationTimer = null;
                await ProcessVotesAsync(matchState);
            }
        }

        public async Task SubscribeToMatchAsync(string gameCode, int playerId)
        {
            if (!matches.TryGetValue(gameCode, out MatchState matchState)) throw new FaultException("The match does not exist or has already ended.");
            if (matchState.Status != MatchStatus.WaitingForPlayers) throw new FaultException("The match has already started or is finishing.");
            var expectedPlayer = matchState.ExpectedPlayers.FirstOrDefault(p => p.Id == playerId);
            if (expectedPlayer == null) throw new FaultException("You are not authorized to join this match.");
            if (matchState.ActivePlayers.ContainsKey(playerId))
            {
                var errorDetail = new ServiceErrorDetailDTO { Code = ServiceErrorCode.AlreadyInRoom, ErrorCode = "PLAYER_ALREADY_IN_MATCH" };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.ErrorCode));
            }
            var callback = operationContext.GetCallbackChannel<IGameManagerCallback>();
            if (!matchState.ActivePlayers.TryAdd(playerId, (callback, expectedPlayer))) throw new FaultException("Internal error while joining the match.");
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
                log.Error($"Error starting match {matchState.GameCode}", ex);
                await BroadcastAsync(matchState, callback => callback.OnMatchCancelled("Error starting the match."));
                matches.TryRemove(matchState.GameCode, out _);
            }
        }

        private async Task StartNewRoundAsync(MatchState matchState)
        {
            // --- REFACTORED METHOD ---
            matchState.Status = MatchStatus.InProgress;
            matchState.CurrentRound++;

            // ADDED: Swap player roles after the first round.
            if (matchState.CurrentRound > 1)
            {
                foreach (var playerEntry in matchState.ActivePlayers.Values)
                {
                    playerEntry.Player.Role = (playerEntry.Player.Role == PlayerRole.ClueGuy) ? PlayerRole.Guesser : PlayerRole.ClueGuy;
                }
            }

            // ADDED: Notify clients about the new round and potential role changes.
            var roundStartState = new RoundStartStateDTO
            {
                CurrentRound = matchState.CurrentRound,
                PlayersWithNewRoles = matchState.ActivePlayers.Values.Select(p => p.Player).ToList()
            };
            await BroadcastAsync(matchState, cb => cb.OnNewRoundStarted(roundStartState));

            matchState.RedTeamWords = await wordRepository.GetRandomWordsAsync(WORDS_PER_ROUND);
            matchState.BlueTeamWords = await wordRepository.GetRandomWordsAsync(WORDS_PER_ROUND);
            matchState.RedTeamWordIndex = 0;
            matchState.BlueTeamWordIndex = 0;
            matchState.RedTeamTurnHistory.Clear();
            matchState.BlueTeamTurnHistory.Clear();
            matchState.RedTeamPassedThisRound = false; // Reset pass flags.
            matchState.BlueTeamPassedThisRound = false;

            if (matchState.RedTeamWords.Count < WORDS_PER_ROUND || matchState.BlueTeamWords.Count < WORDS_PER_ROUND)
            {
                await BroadcastAsync(matchState, cb => cb.OnMatchCancelled("Error: Not enough words found in the database."));
                matches.TryRemove(matchState.GameCode, out _);
                return;
            }

            matchState.SecondsLeft = ROUND_DURATION_SECONDS;
            matchState.RoundTimer = new Timer(TimerTickCallback, matchState, 1000, 1000);

            var redClueGuy = GetPlayerByRole(matchState, MatchTeam.RedTeam, PlayerRole.ClueGuy);
            var redGuesser = GetPlayerByRole(matchState, MatchTeam.RedTeam, PlayerRole.Guesser);
            var blueClueGuy = GetPlayerByRole(matchState, MatchTeam.BlueTeam, PlayerRole.ClueGuy);
            var blueGuesser = GetPlayerByRole(matchState, MatchTeam.BlueTeam, PlayerRole.Guesser);

            // Enviar a Equipo Rojo
            var redWord = matchState.GetCurrentPassword(MatchTeam.RedTeam);
            if (redClueGuy.Callback != null)
            {
                try { redClueGuy.Callback.OnNewPassword(ToDTO(redWord)); }
                catch { await HandlePlayerDisconnectionAsync(matchState, redClueGuy.Player.Id); }
            }
            if (redGuesser.Callback != null)
            {
                try { redGuesser.Callback.OnNewPassword(ToDTOForGuesser(redWord)); } // <-- Envío al Adivinador
                catch { await HandlePlayerDisconnectionAsync(matchState, redGuesser.Player.Id); }
            }

            // Enviar a Equipo Azul
            var blueWord = matchState.GetCurrentPassword(MatchTeam.BlueTeam);
            if (blueClueGuy.Callback != null)
            {
                try { blueClueGuy.Callback.OnNewPassword(ToDTO(blueWord)); }
                catch { await HandlePlayerDisconnectionAsync(matchState, blueClueGuy.Player.Id); }
            }
            if (blueGuesser.Callback != null)
            {
                try { blueGuesser.Callback.OnNewPassword(ToDTOForGuesser(blueWord)); } // <-- Envío al Adivinador
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

            // ADDED: Start a timer for the voting phase to prevent deadlocks.
            matchState.ValidationSecondsLeft = VALIDATION_DURATION_SECONDS;
            matchState.ValidationTimer = new Timer(ValidationTimerTickCallback, matchState, 1000, 1000);
        }

        // --- NEW METHOD ---
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
                // Time's up, process whatever votes we have.
                await ProcessVotesAsync(matchState);
            }
        }


        private async Task ProcessVotesAsync(MatchState matchState)
        {
            // ADDED: Ensure the validation timer is stopped if it wasn't already.
            matchState.ValidationTimer?.Dispose();
            matchState.ValidationTimer = null;

            // The rest of the method is the same...
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
                    // Si el votante es del Equipo Rojo, está votando por las palabras del Equipo Azul.
                    if (voterTeam == MatchTeam.RedTeam)
                    {
                        if (vote.PenalizeMultiword) blueTurnsToPenalizeMultiword.Add(vote.TurnId);
                        if (vote.PenalizeSynonym) blueTurnsToPenalizeSynonym.Add(vote.TurnId);
                    }
                    // Si el votante es del Equipo Azul, está votando por las palabras del Equipo Rojo.
                    else // (voterTeam == MatchTeam.BlueTeam)
                    {
                        if (vote.PenalizeMultiword) redTurnsToPenalizeMultiword.Add(vote.TurnId);
                        if (vote.PenalizeSynonym) redTurnsToPenalizeSynonym.Add(vote.TurnId);
                    }
                }
            }

            redTeamPenalty = (redTurnsToPenalizeMultiword.Count * PENALTY_MULTIWORD) + (redTurnsToPenalizeSynonym.Count * PENALTY_SYNONYM);
            blueTeamPenalty = (blueTurnsToPenalizeMultiword.Count * PENALTY_MULTIWORD) + (blueTurnsToPenalizeSynonym.Count * PENALTY_SYNONYM);
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
            await BroadcastAsync(matchState, cb => cb.OnValidationComplete(validationResult));
            if (matchState.CurrentRound >= TOTAL_ROUNDS)
            {
                await EndGameAsync(matchState);
            }
            else
            {
                await StartNewRoundAsync(matchState);
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
                await BroadcastAsync(matchState, cb => cb.OnMatchCancelled($"Player {disconnectedPlayer.Player.Nickname} has disconnected."));
                matches.TryRemove(matchState.GameCode, out _);
            }
        }

        // --- Helper and Private Methods (no significant changes needed below) ---
        private async Task EndGameAsync(MatchState matchState)
        {
            if (matchState.RedTeamScore == matchState.BlueTeamScore)
            {
                // <-- CAMBIO CLAVE: En lugar de terminar, inicia la muerte súbita.
                await StartSuddenDeathAsync(matchState);
                return; // Stop execution to avoid finishing the match.
            }
            matchState.Status = MatchStatus.Finished;
            MatchTeam? winner = (matchState.RedTeamScore > matchState.BlueTeamScore) ? MatchTeam.RedTeam : MatchTeam.BlueTeam;
            await PersistAndNotifyGameEnd(matchState, winner);
        }
        // En la clase GameManager
        private async Task StartSuddenDeathAsync(MatchState matchState)
        {
            matchState.Status = MatchStatus.SuddenDeath;

            // Notify clients that the tiebreaker is starting.
            await BroadcastAsync(matchState, cb => cb.OnSuddenDeathStarted());

            // Get only ONE word for each team.
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
            matchState.RedTeamTurnHistory.Clear(); // Clear history for the new phase.
            matchState.BlueTeamTurnHistory.Clear();
            matchState.RedTeamPassedThisRound = true; // Disable passing during sudden death.
            matchState.BlueTeamPassedThisRound = true;

            
            matchState.SecondsLeft = SUDDEN_DEATH_DURATION_SECONDS;
            matchState.RoundTimer = new Timer(TimerTickCallback, matchState, 1000, 1000);

            // Send the single word to each team's ClueGuy.
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
            try
            {
                await matchRepository.SaveMatchResultAsync(matchState.RedTeamScore, matchState.BlueTeamScore,
                    redTeamPlayers.Select(p => p.Id), blueTeamPlayers.Select(p => p.Id));
                if (winner.HasValue)
                {
                    var winningPlayerIds = (winner == MatchTeam.RedTeam) ? redTeamPlayers.Select(p => p.Id) : blueTeamPlayers.Select(p => p.Id);
                    await Task.WhenAll(winningPlayerIds.Select(id => playerRepository.UpdatePlayerTotalPointsAsync(id, POINTS_PER_WIN)));
                }
            }
            catch (Exception ex) { log.Error($"ERROR persisting match {matchState.GameCode}: {ex.Message}"); }
            var summary = new MatchSummaryDTO { WinnerTeam = winner, RedScore = matchState.RedTeamScore, BlueScore = matchState.BlueTeamScore };
            await BroadcastAsync(matchState, cb => cb.OnMatchOver(summary));
            if (matches.TryRemove(matchState.GameCode, out var removedMatch)) removedMatch.Dispose();
        }

        private static async Task BroadcastAsync(MatchState game, Action<IGameManagerCallback> action)
        {
            var tasks = game.ActivePlayers.Select(playerEntry => Task.Run(() => {
                try { action(playerEntry.Value.Callback); }
                catch (Exception) { game.ActivePlayers.TryRemove(playerEntry.Key, out _); }
            }));
            await Task.WhenAll(tasks);
        }

        private static async Task BroadcastToPlayersAsync(IEnumerable<(IGameManagerCallback Callback, PlayerDTO Player)> players, Action<IGameManagerCallback> action)
        {
            var tasks = players.Select(playerEntry => Task.Run(() => {
                try { action(playerEntry.Callback); } catch { /* Ignore disconnection here */ }
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

            // El Adivinador recibe las descripciones, pero NO la palabra.
            return new PasswordWordDTO
            {
                EnglishWord = string.Empty, // O string.Empty, como prefieras
                SpanishWord = string.Empty, // O string.Empty
                EnglishDescription = entity.EnglishDescription,
                SpanishDescription = entity.SpanishDescription
            };
        }

        private static IEnumerable<(IGameManagerCallback Callback, PlayerDTO Player)> GetPlayersByTeam(MatchState state, MatchTeam team) => state.ActivePlayers.Values.Where(p => p.Player.Team == team);
        private static (IGameManagerCallback Callback, PlayerDTO Player) GetPlayerByRole(MatchState state, MatchTeam team, PlayerRole role) => state.ActivePlayers.Values.FirstOrDefault(p => p.Player.Team == team && p.Player.Role == role);
        private static (IGameManagerCallback Callback, PlayerDTO Player) GetPartner(MatchState state, (IGameManagerCallback Callback, PlayerDTO Player) player) => state.ActivePlayers.Values.FirstOrDefault(p => p.Player.Team == player.Player.Team && p.Player.Id != player.Player.Id);
    }
}