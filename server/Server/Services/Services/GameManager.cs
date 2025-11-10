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

            // --- Lógica para Juego Simultáneo ---
            public List<PasswordWord> RedTeamWords { get; set; }
            public List<PasswordWord> BlueTeamWords { get; set; }
            public int RedTeamWordIndex { get; set; }
            public int BlueTeamWordIndex { get; set; }
            public List<TurnHistoryDTO> RedTeamTurnHistory { get; set; }
            public List<TurnHistoryDTO> BlueTeamTurnHistory { get; set; }
            // -------------------------------------
            
            public List<List<ValidationVoteDTO>> ReceivedVotes { get; }
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
                CurrentRound = 0; // Se incrementará a 1 al iniciar la primera ronda

                ReceivedVotes = new List<List<ValidationVoteDTO>>();
                PlayersWhoVoted = new HashSet<int>();
            }
            public PasswordWord GetCurrentPassword(MatchTeam team)
            {
                if (team == MatchTeam.RedTeam)
                {
                    if (RedTeamWordIndex < RedTeamWords.Count)
                    {
                        return RedTeamWords[RedTeamWordIndex];
                    }
                }
                else
                {
                    if (BlueTeamWordIndex < BlueTeamWords.Count)
                    {
                        return BlueTeamWords[BlueTeamWordIndex];
                    }
                }
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
            if (!matches.TryGetValue(gameCode, out MatchState matchState) || matchState.Status != MatchStatus.InProgress)
                return;
            if (!matchState.ActivePlayers.TryGetValue(senderPlayerId, out var sender))
                return;
            if (sender.Player.Role != PlayerRole.ClueGuy)
                return;

            var team = sender.Player.Team;
            var currentPassword = matchState.GetCurrentPassword(team);
            var historyItem = new TurnHistoryDTO
            {
                TurnId = (team == MatchTeam.RedTeam) ? matchState.RedTeamWordIndex : matchState.BlueTeamWordIndex,
                Password = ToDTO(currentPassword), // DTO con palabra en ambos idiomas
                ClueUsed = clue
            };

            // Añadir al historial del equipo correcto
            if (team == MatchTeam.RedTeam)
                matchState.RedTeamTurnHistory.Add(historyItem);
            else
                matchState.BlueTeamTurnHistory.Add(historyItem);

            // Enviar la pista solo al Adivinador del equipo
            var partner = GetPartner(matchState, sender);
            if (partner.Callback != null)
            {
                try { partner.Callback.OnClueReceived(clue); }
                catch { await HandlePlayerDisconnectionAsync(matchState, partner.Player.Id); }
            }
        }

        public async Task SubmitGuessAsync(string gameCode, int senderPlayerId, string guess)
        {
            if (!matches.TryGetValue(gameCode, out MatchState matchState) || matchState.Status != MatchStatus.InProgress)
                return;
            if (!matchState.ActivePlayers.TryGetValue(senderPlayerId, out var sender))
                return;
            if (sender.Player.Role != PlayerRole.Guesser)
                return;

            var team = sender.Player.Team;
            var currentPassword = matchState.GetCurrentPassword(team);
            int currentScore = (team == MatchTeam.RedTeam) ? matchState.RedTeamScore : matchState.BlueTeamScore;

            // Comparar Adivinanza
            if (currentPassword != null && (guess.Equals(currentPassword.EnglishWord, StringComparison.OrdinalIgnoreCase)
                 || guess.Equals(currentPassword.SpanishWord, StringComparison.OrdinalIgnoreCase)))
            {
                // --- ÉXITO ---
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
                await BroadcastAsync(matchState, cb => cb.OnGuessResult(resultDto)); // Notificar a todos

                // Avanzar la palabra para ese equipo
                if (team == MatchTeam.RedTeam)
                    matchState.RedTeamWordIndex++;
                else
                    matchState.BlueTeamWordIndex++;

                // Enviar la siguiente palabra *solo* al Pistero de ese equipo
                var nextWord = matchState.GetCurrentPassword(team);
                if (nextWord != null)
                {
                    var pistero = GetPlayerByRole(matchState, team, PlayerRole.ClueGuy);
                    if (pistero.Callback != null)
                    {
                        try { pistero.Callback.OnNewPassword(ToDTO(nextWord)); }
                        catch { await HandlePlayerDisconnectionAsync(matchState, pistero.Player.Id); }
                    }
                }
                // Si nextWord es null, el equipo terminó sus 5 palabras.
            }
            else
            {
                // --- FALLO ---
                var resultDto = new GuessResultDTO { IsCorrect = false, Team = team, NewScore = currentScore };
                // Notificar solo al equipo activo (Pistero y Adivinador)
                var pistero = GetPlayerByRole(matchState, team, PlayerRole.ClueGuy);
                try { sender.Callback.OnGuessResult(resultDto); }
                catch { await HandlePlayerDisconnectionAsync(matchState, sender.Player.Id); }
                try { pistero.Callback?.OnGuessResult(resultDto); }
                catch { await HandlePlayerDisconnectionAsync(matchState, pistero.Player.Id); }
            }
        }

        public async Task SubmitValidationVotesAsync(string gameCode, int senderPlayerId, List<ValidationVoteDTO> votes)
        {
            if (!matches.TryGetValue(gameCode, out MatchState matchState) || matchState.Status != MatchStatus.Validating)
                return;
            if (!matchState.ActivePlayers.TryGetValue(senderPlayerId, out var sender))
                return;

            lock (matchState.ReceivedVotes)
            {
                if (matchState.PlayersWhoVoted.Contains(senderPlayerId))
                    return; // Ya votó

                matchState.PlayersWhoVoted.Add(senderPlayerId);
                matchState.ReceivedVotes.Add(votes); // Agregamos los votos

                // Esperamos los votos de los 4 jugadores
                if (matchState.PlayersWhoVoted.Count < 4)
                {
                    return; // Faltan votos
                }
            }

            // Si tenemos 4 sets de votos, procesamos
            await ProcessVotesAsync(matchState);
        }

        public async Task SubscribeToMatchAsync(string gameCode, int playerId)
        {
            if (!matches.TryGetValue(gameCode, out MatchState matchState))
            {
                throw new FaultException("La partida no existe o ya ha terminado.");
            }
            if (matchState.Status != MatchStatus.WaitingForPlayers)
            {
                throw new FaultException("La partida ya ha comenzado o está finalizando.");
            }
            var expectedPlayer = matchState.ExpectedPlayers.FirstOrDefault(p => p.Id == playerId);
            if (expectedPlayer == null)
            {
                log.WarnFormat("Intento de unión no autorizado a la partida {0} por el jugador {1}.", gameCode, playerId);
                throw new FaultException("No estás autorizado para unirte a esta partida.");
            }
            if (matchState.ActivePlayers.ContainsKey(playerId))
            {
                var errorDetail = new ServiceErrorDetailDTO
                {
                    Code = ServiceErrorCode.AlreadyInRoom,
                    ErrorCode = "PLAYER_ALREADY_IN_MATCH"
                };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.ErrorCode));
            }

            var callback = operationContext.GetCallbackChannel<IGameManagerCallback>();
            if (!matchState.ActivePlayers.TryAdd(playerId, (callback, expectedPlayer)))
            {
                throw new FaultException("Error interno al unirse a la partida.");
            }

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
                var initState = new MatchInitStateDTO
                {
                    Players = matchState.ActivePlayers.Values.Select(p => p.Player).ToList()
                };
                await BroadcastAsync(matchState, callback => callback.OnMatchInitialized(initState));

                // Inicia la primera ronda
                await StartNewRoundAsync(matchState);
            }
            catch (Exception ex)
            {
                log.Error($"Error al iniciar partida {matchState.GameCode}", ex);
                await BroadcastAsync(matchState, callback => callback.OnMatchCancelled("Error al iniciar la partida."));
                matches.TryRemove(matchState.GameCode, out _);
            }
        }

        private async Task StartNewRoundAsync(MatchState matchState)
        {
            matchState.Status = MatchStatus.InProgress;
            matchState.CurrentRound++; // Ronda 1, 2, 3...

            // 1. Obtener palabras para AMBOS equipos
            matchState.RedTeamWords = await wordRepository.GetRandomWordsAsync(WORDS_PER_ROUND);
            matchState.BlueTeamWords = await wordRepository.GetRandomWordsAsync(WORDS_PER_ROUND);

            matchState.RedTeamWordIndex = 0;
            matchState.BlueTeamWordIndex = 0;
            matchState.RedTeamTurnHistory.Clear();
            matchState.BlueTeamTurnHistory.Clear();

            // 2. Comprobar si la BD falló
            if (matchState.RedTeamWords.Count < WORDS_PER_ROUND || matchState.BlueTeamWords.Count < WORDS_PER_ROUND)
            {
                await BroadcastAsync(matchState, cb => cb.OnMatchCancelled("Error: No se encontraron suficientes palabras en la base de datos."));
                matches.TryRemove(matchState.GameCode, out _);
                return;
            }

            // 3. Iniciar el timer
            matchState.SecondsLeft = ROUND_DURATION_SECONDS;
            matchState.RoundTimer = new Timer(TimerTickCallback, matchState, 1000, 1000);

            // 4. Enviar la primera palabra a AMBOS Pisteros
            var pisteroRojo = GetPlayerByRole(matchState, MatchTeam.RedTeam, PlayerRole.ClueGuy);
            var pisteroAzul = GetPlayerByRole(matchState, MatchTeam.BlueTeam, PlayerRole.ClueGuy);

            if (pisteroRojo.Callback != null)
            {
                try
                {
                    // CORRECCIÓN DE ERROR 1: Pasar el equipo
                    var firstWord = matchState.GetCurrentPassword(MatchTeam.RedTeam);
                    pisteroRojo.Callback.OnNewPassword(ToDTO(firstWord));
                }
                catch { await HandlePlayerDisconnectionAsync(matchState, pisteroRojo.Player.Id); }
            }
            if (pisteroAzul.Callback != null)
            {
                try
                {
                    // CORRECCIÓN DE ERROR 1: Pasar el equipo
                    var firstWord = matchState.GetCurrentPassword(MatchTeam.BlueTeam);
                    pisteroAzul.Callback.OnNewPassword(ToDTO(firstWord));
                }
                catch { await HandlePlayerDisconnectionAsync(matchState, pisteroAzul.Player.Id); }
            }
        }

        private static async Task BroadcastAsync(MatchState game, Action<IGameManagerCallback> action)
        {
            var tasks = game.ActivePlayers.Select(playerEntry =>
                            Task.Run(async () => // Añadido async
                            {
                                try
                                {
                                    action(playerEntry.Value.Callback);
                                }
                                catch (Exception)
                                {
                                    game.ActivePlayers.TryRemove(playerEntry.Key, out _);
                                    // No podemos llamar a HandlePlayerDisconnectionAsync aquí
                                    // porque es un método de instancia y este es estático.
                                    // La desconexión se manejará en la próxima interacción del jugador.
                                }
                            })
                        );
            await Task.WhenAll(tasks);
        }

        /*METODO MODIFICADO METODO ORIGINAL ABAJO
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
        */

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
            matchState.ReceivedVotes.Clear();
            matchState.PlayersWhoVoted.Clear();

            var redTeamPlayers = GetPlayersByTeam(matchState, MatchTeam.RedTeam);
            var blueTeamPlayers = GetPlayersByTeam(matchState, MatchTeam.BlueTeam);

            // Enviar historial de ROJO a equipo AZUL
            if (matchState.RedTeamTurnHistory.Count > 0)
            {
                await BroadcastToPlayersAsync(blueTeamPlayers, cb => cb.OnBeginRoundValidation(matchState.RedTeamTurnHistory));
            }
            // Enviar historial de AZUL a equipo ROJO
            if (matchState.BlueTeamTurnHistory.Count > 0)
            {
                await BroadcastToPlayersAsync(redTeamPlayers, cb => cb.OnBeginRoundValidation(matchState.BlueTeamTurnHistory));
            }

            // Si ningún equipo jugó (ej. 0 pistas), saltar la validación y pasar a la siguiente ronda.
            if (matchState.RedTeamTurnHistory.Count == 0 && matchState.BlueTeamTurnHistory.Count == 0)
            {
                await ProcessVotesAsync(matchState);
                return;
            }
            // TODO: Iniciar un timer de votación
        }

        private async Task HandlePlayerDisconnectionAsync(MatchState matchState, int disconnectedPlayerId)
        {
            if (matchState.Status == MatchStatus.Finished) return;
            if (matchState.ActivePlayers.TryRemove(disconnectedPlayerId, out var disconnectedPlayer))
            {
                matchState.Status = MatchStatus.Finished;
                matchState.RoundTimer?.Dispose();
                matchState.RoundTimer = null;
                await BroadcastAsync(matchState, cb => cb.OnMatchCancelled($"El jugador {disconnectedPlayer.Player.Nickname} se ha desconectado."));
                matches.TryRemove(matchState.GameCode, out _);
            }
        }

        private async Task ProcessVotesAsync(MatchState matchState)
        {
            int redTeamPenalty = 0;
            int blueTeamPenalty = 0;

            var redTurnsToPenalizeMultiword = new HashSet<int>();
            var blueTurnsToPenalizeMultiword = new HashSet<int>();
            var redTurnsToPenalizeSynonym = new HashSet<int>();
            var blueTurnsToPenalizeSynonym = new HashSet<int>();

            foreach (var voteList in matchState.ReceivedVotes)
            {
                foreach (var vote in voteList)
                {
                    // Buscar en qué historial está este turno
                    bool isRedTeamTurn = matchState.RedTeamTurnHistory.Any(t => t.TurnId == vote.TurnId);
                    bool isBlueTeamTurn = matchState.BlueTeamTurnHistory.Any(t => t.TurnId == vote.TurnId);

                    if (isRedTeamTurn)
                    {
                        if (vote.PenalizeMultiword) redTurnsToPenalizeMultiword.Add(vote.TurnId);
                        if (vote.PenalizeSynonym) redTurnsToPenalizeSynonym.Add(vote.TurnId);
                    }
                    else if (isBlueTeamTurn)
                    {
                        if (vote.PenalizeMultiword) blueTurnsToPenalizeMultiword.Add(vote.TurnId);
                        if (vote.PenalizeSynonym) blueTurnsToPenalizeSynonym.Add(vote.TurnId);
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
                TeamThatWasValidated = MatchTeam.RedTeam, // DTO obsoleto, pero lo llenamos
                TotalPenaltyApplied = redTeamPenalty + blueTeamPenalty,
                NewRedTeamScore = matchState.RedTeamScore,
                NewBlueTeamScore = matchState.BlueTeamScore,
                // CORRECCIÓN DE ERROR 2: La propiedad 'Message' no existe en el DTO
                // Message = $"Penalización: Equipo Rojo -{redTeamPenalty}, Equipo Azul -{blueTeamPenalty}"
            };
            await BroadcastAsync(matchState, cb => cb.OnValidationComplete(validationResult));

            // Continuar el flujo del juego
            if (matchState.CurrentRound >= TOTAL_ROUNDS)
            {
                await EndGameAsync(matchState);
            }
            else
            {
                // Iniciar la siguiente ronda
                await StartNewRoundAsync(matchState);
            }
        }

        private static async Task BroadcastToPlayersAsync(IEnumerable<(IGameManagerCallback Callback, PlayerDTO Player)> players, Action<IGameManagerCallback> action)
        {
            var tasks = players.Select(playerEntry =>
                            Task.Run(() =>
                            {
                                try { action(playerEntry.Callback); }
                                catch { /* Ignoramos desconexión aquí */ }
                            })
                        );
            await Task.WhenAll(tasks);
        }

        private async Task EndGameAsync(MatchState matchState)
        {
            matchState.Status = MatchStatus.Finished;

            MatchTeam? winner = null;
            if (matchState.RedTeamScore > matchState.BlueTeamScore)
                winner = MatchTeam.RedTeam;
            else if (matchState.BlueTeamScore > matchState.RedTeamScore)
                winner = MatchTeam.BlueTeam;
            // Si son iguales, winner es null (empate)

            await PersistAndNotifyGameEnd(matchState, winner);
        }

        private async Task PersistAndNotifyGameEnd(MatchState matchState, MatchTeam? winner)
        {
            var redTeamPlayers = GetPlayersByTeam(matchState, MatchTeam.RedTeam).Select(p => p.Player);
            var blueTeamPlayers = GetPlayersByTeam(matchState, MatchTeam.BlueTeam).Select(p => p.Player);
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
                log.Error($"ERROR al persistir partida {matchState.GameCode}: {ex.Message}");
            }

            var summary = new MatchSummaryDTO
            {
                WinnerTeam = winner,
                RedScore = matchState.RedTeamScore,
                BlueScore = matchState.BlueTeamScore
            };

            await BroadcastAsync(matchState, cb => cb.OnMatchOver(summary));

            if (matches.TryRemove(matchState.GameCode, out var removedMatch))
            {
                removedMatch.Dispose();
            }
        }

        private PasswordWordDTO ToDTO(PasswordWord entity)
        {
            if (entity == null) return new PasswordWordDTO { SpanishWord = "FIN", EnglishWord = "END" };

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
            return state.ActivePlayers.Values.FirstOrDefault(p => p.Player.Team == player.Player.Team && p.Player.Id != player.Player.Id);
        }
    }
}
