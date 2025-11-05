using Data.DAL.Interfaces;
using Data.Model;
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

            // Players from the waiting room who are expected to join this match
            public List<PlayerDTO> ExpectedPlayers { get; }

            // Players who have joined the match
            public ConcurrentDictionary<int, (IGameManagerCallback Callback, PlayerDTO Player)> ActivePlayers { get; }

            public int RedTeamScore { get; set; }
            public int BlueTeamScore { get; set; }
            public Timer RoundTimer { get; set; }
            public int SecondsLeft; // Managed with Interlocked
            public int CurrentRound { get; set; }
            public MatchTeam CurrentTurnTeam { get; set; } // Team currently taking the turn
            public List<string> CurrentRoundWords { get; set; } // The5 words for the round
            public int CurrentWordIndex { get; set; }
            public List<TurnHistoryDTO> CurrentTurnHistory { get; set; }

            public MatchState(string gameCode, List<PlayerDTO> expectedPlayers)
            {
                GameCode = gameCode;
                ExpectedPlayers = expectedPlayers;
                Status = MatchStatus.WaitingForPlayers;
                ActivePlayers = new ConcurrentDictionary<int, (IGameManagerCallback, PlayerDTO)>();
                CurrentRoundWords = new List<string>();
                CurrentTurnHistory = new List<TurnHistoryDTO>();
                RedTeamScore =0;
                BlueTeamScore =0;
                CurrentRound =1;
                CurrentTurnTeam = MatchTeam.RedTeam;
            }
            public string GetCurrentPassword()
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
        private const int ROUND_DURATION_SECONDS =60;
        private const int WORDS_PER_ROUND =5;

        public GameManager(IOperationContextWrapper contextWrapper, IWordRepository wordRepository)
        {
            this.operationContext = contextWrapper;
            this.wordRepository = wordRepository;
        }
        public bool CreateMatch(string gameCode, List<PlayerDTO> playersFromWaitingRoom)
        {
            if (playersFromWaitingRoom == null || playersFromWaitingRoom.Count !=4)
            {
                // Cannot create the match because there are not exactly4 players
                return false;
            }

            var matchState = new MatchState(gameCode, playersFromWaitingRoom);

            // Add the new match to the dictionary in the "WaitingForPlayers" state
            return matches.TryAdd(gameCode, matchState);
        }
        public Task PassTurnAsync(string gameCode, int senderPlayerId)
        {
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
                // Optional: send a FaultException
                return;
            }

            // Save the clue for later validation
            var currentPassword = matchState.GetCurrentPassword() ?? "N/A";
            matchState.CurrentTurnHistory.Add(new TurnHistoryDTO
            {
                TurnId = matchState.CurrentWordIndex,
                Password = (string)currentPassword,
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
                // Optional: send a FaultException
                return;
            }

            var team = sender.Player.Team;
            var currentPassword = matchState.GetCurrentPassword();
            int currentScore = (team == MatchTeam.RedTeam) ? matchState.RedTeamScore : matchState.BlueTeamScore;

            // Compare the guess
            if (currentPassword != null && guess.Equals((string)currentPassword, StringComparison.OrdinalIgnoreCase))
            {
                // --- SUCCESS CASE ---
                int newScore;
                if (team == MatchTeam.RedTeam)
                {
                    // Use a local variable to hold the field reference
                    var redScore = matchState.RedTeamScore;
                    newScore = Interlocked.Increment(ref redScore);
                    matchState.RedTeamScore = redScore;
                }
                else
                {
                    var blueScore = matchState.BlueTeamScore;
                    newScore = Interlocked.Increment(ref blueScore);
                    matchState.BlueTeamScore = blueScore;
                }

                var resultDto = new GuessResultDTO { IsCorrect = true, Team = team, NewScore = newScore };
                await BroadcastAsync(matchState, cb => cb.OnGuessResult(resultDto)); // Notify everyone

                matchState.CurrentWordIndex++; // Move to the next word

                if (matchState.CurrentWordIndex >= matchState.CurrentRoundWords.Count)
                {
                    // The5 words are done. End the round.
                    matchState.RoundTimer?.Dispose();
                    matchState.RoundTimer = null;
                    await StartValidationPhaseAsync(matchState);
                }
                else
                {
                    // Send the next word to the ClueGuy
                    var pistero = GetPlayerByRole(matchState, team, PlayerRole.ClueGuy);
                    if (pistero.Callback != null)
                    {
                        try
                        {
                            var nextWord = (string) matchState.GetCurrentPassword();
                            pistero.Callback.OnNewPassword(nextWord);
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
                try { sender.Callback.OnGuessResult(resultDto); } // Guesser
                catch { await HandlePlayerDisconnectionAsync(matchState, sender.Player.Id); }
                try { if (pistero.Callback != null) pistero.Callback.OnGuessResult(resultDto); } // ClueGuy
                catch { await HandlePlayerDisconnectionAsync(matchState, pistero.Player.Id); }
            }
        }

        public Task SubmitValidationVotesAsync(string gameCode, int senderPlayerId, List<ValidationVoteDTO> votes)
        {
            return Task.CompletedTask;
        }

        public async Task SubscribeToMatchAsync(string gameCode, int playerId)
        {
            if (!matches.TryGetValue(gameCode, out var matchState))
            {
                throw new FaultException("La partida no existe o ya ha terminado.");
            }

            if (matchState.Status != MatchStatus.WaitingForPlayers)
            {
                throw new FaultException("La partida ya ha comenzado o está finalizando.");
            }

            // Validate that the player is one of the expected players
            var expectedPlayer = matchState.ExpectedPlayers.FirstOrDefault(p => p.Id == playerId);
            if (expectedPlayer == null)
            {
                throw new FaultException("No estás autorizado para unirte a esta partida.");
            }

            if (matchState.ActivePlayers.ContainsKey(playerId))
            {
                throw new FaultException("Ya estás conectado a esta partida.");
            }

            var callback = operationContext.GetCallbackChannel<IGameManagerCallback>();
            if (!matchState.ActivePlayers.TryAdd(playerId, (callback, expectedPlayer)))
            {
                throw new FaultException("Error interno al unirse a la partida.");
            }

            // TASK2.1.4 (Key logic): Check if this is the last player to join
            if (matchState.ActivePlayers.Count == matchState.ExpectedPlayers.Count)
            {
                // Everyone is connected! Start the match.
                await StartGameInternalAsync(matchState);
            }
        }

        private async Task StartGameInternalAsync(MatchState matchState)
        {
            try
            {
                matchState.Status = MatchStatus.InProgress;
                matchState.CurrentRound =1;

                var initState = new MatchInitStateDTO
                {
                    Players = matchState.ActivePlayers.Values.Select(p => p.Player).ToList()
                };

                await BroadcastAsync(matchState, callback => callback.OnMatchInitialized(initState));
                await StartRoundAsync(matchState);
            }
            catch (Exception ex)
            {
                // If something fails (e.g., no words in the DB), cancel the match
                // Log(ex);
                await BroadcastAsync(matchState, callback => callback.OnMatchCancelled("Error al iniciar la partida."));
                matches.TryRemove(matchState.GameCode, out _);
            }
        }

        private async Task BroadcastAsync(MatchState game, Action<IGameManagerCallback> action)
        {
            var tasks = game.ActivePlayers.Select(async playerEntry =>
            {
                try
                {
                    action(playerEntry.Value.Callback);
                }
                catch
                {
                    // TASK5.0 (Disconnection handling)
                    // If broadcasting fails, the player got disconnected.
                    // You should handle this (e.g., `await HandlePlayerDisconnectionAsync(...)`)
                    game.ActivePlayers.TryRemove(playerEntry.Key, out _);
                }
            });
            await Task.WhenAll(tasks);
        }
        private async Task StartRoundAsync(MatchState matchState)
        {
            matchState.Status = MatchStatus.InProgress;

            // TASK3.1.1: Load5 words
            matchState.CurrentRoundWords = await wordRepository.GetRandomWordsAsync(WORDS_PER_ROUND);
            matchState.CurrentWordIndex =0;
            matchState.CurrentTurnHistory = new List<TurnHistoryDTO>();

            if (matchState.CurrentRoundWords.Count ==0)
            {
                await BroadcastAsync(matchState, cb => cb.OnMatchCancelled("Error: No se encontraron palabras en la base de datos."));
                return;
            }

            // TASK3.1.3: Start timer
            matchState.SecondsLeft = ROUND_DURATION_SECONDS;
            // Start the timer. Calls TimerTickCallback for the first time after1s, then every1s.
            matchState.RoundTimer = new Timer(TimerTickCallback, matchState,1000,1000);

            // TASK3.1.4: Send the first word to the current team's ClueGuy
            var pistero = GetPlayerByRole(matchState, matchState.CurrentTurnTeam, PlayerRole.ClueGuy);
            if (pistero.Callback != null)
            {
                try
                {
                    var firstWord = (string)matchState.GetCurrentPassword();
                    pistero.Callback.OnNewPassword(firstWord);
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

            if (newTime <=0)
            {
                matchState.RoundTimer?.Dispose();
                matchState.RoundTimer = null;
                // Time is up. Start the validation phase
                await StartValidationPhaseAsync(matchState);
            }
        }

        // --- STUBS (Future tasks) ---
        
        private async Task StartValidationPhaseAsync(MatchState matchState)
        {
            // TODO: Level4.1
            // This function will be called when time runs out or the5 words are completed.
            // It will stop the game and notify the opposing team to validate.
            matchState.Status = MatchStatus.Validating;
            // await BroadcastAsync(...);
        }

        private async Task HandlePlayerDisconnectionAsync(MatchState matchState, int disconnectedPlayerId)
        {
            // TODO: Level5+ (Disconnection handling)
            if (matchState.ActivePlayers.TryRemove(disconnectedPlayerId, out var disconnectedPlayer))
            {
                matchState.Status = MatchStatus.Finished;
                matchState.RoundTimer?.Dispose();
                matchState.RoundTimer = null;
                await BroadcastAsync(matchState, cb => cb.OnMatchCancelled($"El jugador {disconnectedPlayer.Player.Nickname} se ha desconectado."));
                matches.TryRemove(matchState.GameCode, out _);
            }
        }

        // --- Helpers ---
        private (IGameManagerCallback Callback, PlayerDTO Player) GetPlayerByRole(MatchState state, MatchTeam team, PlayerRole role)
        {
            return state.ActivePlayers.Values.FirstOrDefault(p => p.Player.Team == team && p.Player.Role == role);
        }

        private (IGameManagerCallback Callback, PlayerDTO Player) GetPartner(MatchState state, (IGameManagerCallback Callback, PlayerDTO Player) player)
        {
            // Access the Player property from the tuple parameter correctly
            return state.ActivePlayers.Values.FirstOrDefault(p => p.Player.Team == player.Player.Team && p.Player.Id != player.Player.Id);
        }
    }
}
