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
            public List<PasswordWord> CurrentRoundWords { get; set; } // The 5 words for the round
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
        private const int ROUND_DURATION_SECONDS = 60;
        private const int WORDS_PER_ROUND = 5;
        private const int TOTAL_ROUNDS = 5;

        public GameManager(IOperationContextWrapper contextWrapper, IWordRepository wordRepository)
        {
            this.operationContext = contextWrapper;
            this.wordRepository = wordRepository;
        }
        public bool CreateMatch(string gameCode, List<PlayerDTO> playersFromWaitingRoom)
        {
            if (playersFromWaitingRoom == null || playersFromWaitingRoom.Count !=4)
            {
                // Cannot create the match because there are not exactly 4 players
                return false;
            }

            var matchState = new MatchState(gameCode, playersFromWaitingRoom);

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
                // Optional: send a FaultException
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

                if (matchState.CurrentWordIndex >= matchState.CurrentRoundWords.Count)
                {
                    // The 5 words are done. End the round.
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
                try { 
                    sender.Callback.OnGuessResult(resultDto); 
                } // Guesser
                catch { 
                    await HandlePlayerDisconnectionAsync(matchState, sender.Player.Id); 
                }
                try {
                    pistero.Callback?.OnGuessResult(resultDto);
                } // ClueGuy
                catch { 
                    await HandlePlayerDisconnectionAsync(matchState, pistero.Player.Id); 
                }
            }
        }

        public async Task SubmitValidationVotesAsync(string gameCode, int senderPlayerId, List<ValidationVoteDTO> votes)
        {
            if (!matches.TryGetValue(gameCode, out var matchState) || matchState.Status != MatchStatus.Validating)
                return; // No es tiempo de votar

            if (!matchState.ActivePlayers.TryGetValue(senderPlayerId, out var sender))
                return; // Jugador no existe

            var teamThatPlayed = matchState.CurrentTurnTeam;
            var validatingTeam = (teamThatPlayed == MatchTeam.RedTeam) ? MatchTeam.BlueTeam : MatchTeam.RedTeam;

            if (sender.Player.Team != validatingTeam)
                return; // El equipo incorrecto está intentando votar

            // Tarea 4.3.2: Sincronización
            lock (matchState.ReceivedVotes)
            {
                if (matchState.PlayersWhoVoted.Contains(senderPlayerId))
                    return; // Ya votó

                matchState.PlayersWhoVoted.Add(senderPlayerId);
                matchState.ReceivedVotes.Add(votes);

                // Comprueba si ya han votado los 2 jugadores
                var validators = GetPlayersByTeam(matchState, validatingTeam);
                if (matchState.PlayersWhoVoted.Count < validators.Count())
                {
                    return; // Faltan votos
                }

                // ¡Tenemos todos los votos!
            }

            // Tarea 4.3.3: Procesar los votos
            // (Lo llamamos fuera del lock para evitar mantenerlo bloqueado durante operaciones async)
            await ProcessVotesAsync(matchState, teamThatPlayed);
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
                matchState.CurrentRound = 1;

                var initState = new MatchInitStateDTO
                {
                    Players = matchState.ActivePlayers.Values.Select(p => p.Player).ToList()
                };

                await BroadcastAsync(matchState, callback => callback.OnMatchInitialized(initState));
                await StartRoundTurnAsync(matchState);
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

        //METODO MODIFICADO METODO ORIGINAL ABAJO
        private async Task StartRoundTurnAsync(MatchState matchState)
        {
            matchState.Status = MatchStatus.InProgress;

            matchState.CurrentRoundWords = await wordRepository.GetRandomWordsAsync(WORDS_PER_ROUND); 
            matchState.CurrentWordIndex = 0;
            matchState.CurrentTurnHistory = new List<TurnHistoryDTO>();

            if (matchState.CurrentRoundWords == null || matchState.CurrentRoundWords.Count < WORDS_PER_ROUND)
            {
                await BroadcastAsync(matchState, cb => cb.OnMatchCancelled("Error: No se encontraron palabras en la base de datos."));
                matches.TryRemove(matchState.GameCode, out _);
                return;
            }

            matchState.SecondsLeft = ROUND_DURATION_SECONDS;
            matchState.RoundTimer = new Timer(TimerTickCallback, matchState, 1000, 1000);

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
        /*
                private async Task StartRoundTurnAsync(MatchState matchState)
        {
            matchState.Status = MatchStatus.InProgress;
            matchState.CurrentRoundWords = await wordRepository.GetRandomWordsAsync(WORDS_PER_ROUND);
            matchState.CurrentWordIndex = 0;
            matchState.CurrentTurnHistory = new List<TurnHistoryDTO>();
            if (matchState.CurrentTurnTeam == MatchTeam.RedTeam)
            {
                matchState.CurrentRoundWords = await wordRepository.GetRandomWordsAsync(WORDS_PER_ROUND);
            }

            matchState.CurrentWordIndex = 0;
            matchState.CurrentTurnHistory = new List<TurnHistoryDTO>();

            if (matchState.CurrentRoundWords == null || matchState.CurrentRoundWords.Count == 0)
            {
                await BroadcastAsync(matchState, cb => cb.OnMatchCancelled("Error: No se encontraron palabras en la base de datos."));
                return;
            }

            matchState.SecondsLeft = ROUND_DURATION_SECONDS;
            matchState.RoundTimer = new Timer(TimerTickCallback, matchState,1000,1000);

            // TASK3.1.4: Send the first word to the current team's ClueGuy
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
        */

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
                await StartValidationPhaseAsync(matchState);
            }
        }

        // --- STUBS (Future tasks) ---
        
        private async Task StartValidationPhaseAsync(MatchState matchState)
        {
            matchState.Status = MatchStatus.Validating;
            var teamThatPlayed = matchState.CurrentTurnTeam;
            var validatingTeam = (teamThatPlayed == MatchTeam.RedTeam) ? MatchTeam.BlueTeam : MatchTeam.RedTeam;

            // Limpia los votos de la validación anterior
            matchState.ReceivedVotes.Clear();
            matchState.PlayersWhoVoted.Clear();

            // Tarea 4.1.2: Obtener historial
            var historyToValidate = matchState.CurrentTurnHistory;
            if (historyToValidate.Count == 0)
            {
                // No hay nada que validar (ej. se acabó el tiempo en la primera palabra sin pistas)
                // Pasa directamente a procesar los "votos" (que están vacíos)
                await ProcessVotesAsync(matchState, teamThatPlayed);
                return;
            }

            // Tarea 4.1.3: Enviar historial al equipo validador
            var validators = GetPlayersByTeam(matchState, validatingTeam);
            await BroadcastToPlayersAsync(validators, cb => cb.OnBeginRoundValidation(historyToValidate));

            // Opcional: Iniciar un timer de votación (ej. 30 segundos)
            // Si el timer expira, llama a ProcessVotesAsync forzosamente.
        }

        private async Task HandlePlayerDisconnectionAsync(MatchState matchState, int disconnectedPlayerId)
        {
            if (matchState.Status == MatchStatus.Finished) return;
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
        private IEnumerable<(IGameManagerCallback Callback, PlayerDTO Player)> GetPlayersByTeam(MatchState state, MatchTeam team)
        {
            return state.ActivePlayers.Values.Where(p => p.Player.Team == team);
        }
        private (IGameManagerCallback Callback, PlayerDTO Player) GetPlayerByRole(MatchState state, MatchTeam team, PlayerRole role)
        {
            return state.ActivePlayers.Values.FirstOrDefault(p => p.Player.Team == team && p.Player.Role == role);
        }

        private (IGameManagerCallback Callback, PlayerDTO Player) GetPartner(MatchState state, (IGameManagerCallback Callback, PlayerDTO Player) player)
        {
            // Access the Player property from the tuple parameter correctly
            return state.ActivePlayers.Values.FirstOrDefault(p => p.Player.Team == player.Player.Team && p.Player.Id != player.Player.Id);
        }
        private async Task ProcessVotesAsync(MatchState matchState, MatchTeam teamThatPlayed)
        {
            int totalPenalty = 0;
            // (Regla: 2 puntos por sinónimo, 1 punto por multi-palabra, como definimos)
            // (Regla: Se aplica la penalización si *CUALQUIER* validador la marca)

            var penalizedTurns = new HashSet<int>();

            // NOTA: Como no usas sinónimos, quitamos esa lógica, 
            // pero mantenemos la de "multi-palabra" (más de una palabra por pista).
            // Tu regla dice: "El 'Pistero' no tiene permitido decir más de una palabra por turno,
            // sino el equipo será sancionado con un punto menos"

            // Simplificamos la lógica de votación ya que no hay sinónimos:
            // El DTO 'ValidationVoteDTO' solo necesita un bool 'PenalizeMultiword'
            var turnsToPenalize = new HashSet<int>();

            foreach (var voteList in matchState.ReceivedVotes)
            {
                foreach (var vote in voteList.Where(v => v.PenalizeMultiword)) // Asumiendo que ValidationVoteDTO tiene 'PenalizeMultiword'
                {
                    turnsToPenalize.Add(vote.TurnId);
                }
            }

            totalPenalty = turnsToPenalize.Count; // 1 punto por cada turno penalizado

            // Tarea 4.3.3: Aplicar penalización
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

            // Notificar a todos el resultado de la validación
            var validationResult = new ValidationResultDTO
            {
                TeamThatWasValidated = teamThatPlayed,
                TotalPenaltyApplied = totalPenalty,
                NewRedTeamScore = matchState.RedTeamScore,
                NewBlueTeamScore = matchState.BlueTeamScore,
                Message = $"Penalización de {totalPenalty} puntos al Equipo {teamThatPlayed}"
            };
            await BroadcastAsync(matchState, cb => cb.OnValidationComplete(validationResult));

            // Tarea 4.3.4: Continuar el flujo del juego
            if (teamThatPlayed == MatchTeam.RedTeam)
            {
                // El Equipo Rojo acaba de jugar. Ahora es turno del Equipo Azul.
                matchState.CurrentTurnTeam = MatchTeam.BlueTeam;
                await StartRoundTurnAsync(matchState); // Inicia el turno del Azul
            }
            else
            {
                // El Equipo Azul acaba de jugar. La ronda completa ha terminado.
                matchState.CurrentRound++;

                if (matchState.CurrentRound > TOTAL_ROUNDS)
                {
                    // --- TAREA 5.1 (STUB) ---
                    await EndGameAsync(matchState);
                }
                else
                {
                    // Iniciar la siguiente ronda, volviendo al Equipo Rojo
                    matchState.CurrentTurnTeam = MatchTeam.RedTeam;
                    await StartRoundTurnAsync(matchState);
                }
            }
        }
        private async Task BroadcastToPlayersAsync(IEnumerable<(IGameManagerCallback Callback, PlayerDTO Player)> players, Action<IGameManagerCallback> action)
        {
            // Envía solo a un subconjunto de jugadores (ej. los validadores)
            var tasks = players.Select(async playerEntry =>
            {
                try { action(playerEntry.Callback); }
                catch { /* (Opcional) Manejar desconexión aquí también */ }
            });
            await Task.WhenAll(tasks);
        }
        // --- TAREA 5.1 (STUB) ---
        private async Task EndGameAsync(MatchState matchState)
        {
            matchState.Status = MatchStatus.Finished;
            // TODO: Implementar Nivel 5
            // 1. Comparar matchState.RedTeamScore vs BlueTeamScore
            // 2. Manejar lógica de Muerte Súbita (empate)
            // 3. Crear MatchSummaryDTO
            // 4. Broadcast OnMatchOver
            // 5. Persistir en BD (solo IDs > 0)
            // 6. Limpiar (matches.TryRemove)
            await Task.CompletedTask;
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
    }
}
