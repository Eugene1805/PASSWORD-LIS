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
using System.Threading.Tasks;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class GameManager : IGameManager
    {
        sealed class MatchState
        {
            public string GameCode { get; }
            public MatchStatus Status { get; set; }

            // Players from the lobby who are expected to join this match
            public List<PlayerDTO> ExpectedPlayers { get; }

            // Players who have joined the match
            public ConcurrentDictionary<int, (IGameManagerCallback Callback, PlayerDTO Player)> ActivePlayers { get; }

            // --- Propiedades para la lógica del juego (Nivel 3+) ---
            public List<Object> RoundWords { get; set; } // Palabras de la BD
            public int CurrentWordIndex { get; set; }
            public int CurrentRound { get; set; }
            public System.Threading.Timer RoundTimer { get; set; }
            // ...etc.

            public MatchState(string gameCode, List<PlayerDTO> expectedPlayers)
            {
                GameCode = gameCode;
                ExpectedPlayers = expectedPlayers;
                Status = MatchStatus.WaitingForPlayers;
                ActivePlayers = new ConcurrentDictionary<int, (IGameManagerCallback, PlayerDTO)>();
                RoundWords = new List<Object>();
            }
        }

        private readonly ConcurrentDictionary<string, MatchState> matches = new ConcurrentDictionary<string, MatchState>();
        private readonly IOperationContextWrapper operationContext;
        private readonly IWordRepository wordRepository;

        public GameManager(IOperationContextWrapper contextWrapper, IWordRepository wordRepository)
        {
            this.operationContext = contextWrapper;
            this.wordRepository = wordRepository;
        }
        public bool CreateMatch(string gameCode, List<PlayerDTO> playersFromWaitingRoom)
        {
            if (playersFromWaitingRoom == null || playersFromWaitingRoom.Count != 4)
            {
                // Log: No se puede crear la partida, no hay 4 jugadores.
                return false;
            }

            var matchState = new MatchState(gameCode, playersFromWaitingRoom);

            // Añade la nueva partida al diccionario, en estado "WaitingForPlayers"
            return matches.TryAdd(gameCode, matchState);
        }
        public Task PassTurnAsync(string gameCode)
        {
            throw new NotImplementedException();
        }

        public Task SubmitClueAsync(string gameCode, string clue)
        {
            throw new NotImplementedException();
        }

        public Task SubmitGuessAsync(string gameCode, string guess)
        {
            throw new NotImplementedException();
        }

        public Task SubmitValidationVotesAsync(string gameCode, List<ValidationVoteDTO> votes)
        {
            throw new NotImplementedException();
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

            // Valida que el jugador sea uno de los esperados
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

            // TAREA 2.1.4 (Lógica Clave): Comprobar si es el último jugador
            if (matchState.ActivePlayers.Count == matchState.ExpectedPlayers.Count)
            {
                // ¡Todos están conectados! Iniciar la partida.
                await StartGameInternalAsync(matchState);
            }
        }

        private async Task StartGameInternalAsync(MatchState matchState)
        {
            try
            {
                matchState.Status = MatchStatus.InProgress;
                matchState.CurrentRound = 1;

                // 1. Cargar palabras (Asumiendo 5 palabras por ronda)
                // (Debes implementar IWordRepository y el método GetRandomWordsAsync)
                // matchState.RoundWords = await wordRepository.GetRandomWordsAsync(5);
                // if (matchState.RoundWords.Count == 0) throw new Exception("No se cargaron palabras.");

                // 2. Crear el DTO de inicialización
                var initState = new MatchInitStateDTO
                {
                    Players = matchState.ActivePlayers.Values.Select(p => p.Player).ToList()
                };

                // 3. Transmitir a todos los clientes
                await BroadcastAsync(matchState, callback => callback.OnMatchInitialized(initState));

                // 4. Iniciar Timer y enviar primera palabra
                // ... (Lógica de Nivel 3.1.3 y 3.1.4) ...
                // Ejemplo:
                // matchState.RoundTimer = new System.Threading.Timer(TimerTickCallback, matchState, 1000, 1000);
                // var pisteros = matchState.ActivePlayers.Values.Where(p => p.Player.Role == PlayerRole.ClueGuy);
                // var firstWord = matchState.RoundWords[0].Word;
                // foreach (var pistero in pisteros)
                // {
                //    pistero.Callback.OnNewPassword(firstWord);
                // }
            }
            catch (Exception ex)
            {
                // Si algo falla (ej. no hay palabras en BD), cancela la partida.
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
                    // TAREA 5.0 (Manejo de Desconexión)
                    // Si falla el broadcast, el jugador se desconectó.
                    // Debes manejar esto (ej. `await HandlePlayerDisconnectionAsync(...)`)
                    game.ActivePlayers.TryRemove(playerEntry.Key, out _);
                }
            });
            await Task.WhenAll(tasks);
        }
    }
}
