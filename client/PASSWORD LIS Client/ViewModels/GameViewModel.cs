using PASSWORD_LIS_Client.GameManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class GameViewModel : BaseViewModel
    {
        private bool isLoading = true;
        public bool IsLoading
        {
            get => isLoading;
            set => SetProperty(ref isLoading, value);
        }

        private PlayerRole currentPlayerRole;
        public PlayerRole CurrentPlayerRole
        {
            get => currentPlayerRole;
            set => SetProperty(ref currentPlayerRole, value);
        }

        private int timerSeconds = 60;
        public int TimerSeconds
        {
            get => timerSeconds;
            set => SetProperty(ref timerSeconds, value);
        }

        private string teamName;
        public string TeamName
        {
            get => teamName;
            set => SetProperty(ref teamName, value);
        }

        private string currentRoundText;
        public string CurrentRoundText
        {
            get => currentRoundText;
            set => SetProperty(ref currentRoundText, value);
        }

        private string currentWordCountText;
        public string CurrentWordCountText
        {
            get => currentWordCountText;
            set => SetProperty(ref currentWordCountText, value);
        }

        private string teamPointsText;
        public string TeamPointsText
        {
            get => teamPointsText;
            set => SetProperty(ref teamPointsText, value);
        }

        private string currentPasswordWord = "...";
        public string CurrentPasswordWord
        {
            get => currentPasswordWord;
            set => SetProperty(ref currentPasswordWord, value);
        }

        private string currentClue = "Esperando pista..."; 
        public string CurrentClue
        {
            get => currentClue;
            set => SetProperty(ref currentClue, value);
        }



        private readonly IGameManagerService gameManagerService;
        private readonly IWindowService windowService;
        private readonly string gameCode;
        private readonly PlayerDTO currentPlayer;

        private int currentWordIndex = 1; // Contador de palabras (1 a 5)
        private int currentRoundIndex = 1; // Contador de rondas (1 a 5)
        private const int MaxRounds = 5;  // Rondas totales
        private const int MaxWordsPerRound = 5; // Palabras por ronda

        public GameViewModel(IGameManagerService gameManagerService, IWindowService windowService, string gameCode, WaitingRoomManagerServiceReference.PlayerDTO waitingRoomPlayer)
        {
            this.gameManagerService = gameManagerService;
            this.windowService = windowService;
            this.gameCode = gameCode;

            currentPlayer = new PlayerDTO
            {
                Id = waitingRoomPlayer.Id,
                Nickname = waitingRoomPlayer.Nickname,
                PhotoId = waitingRoomPlayer.PhotoId,
                Role = (PlayerRole)waitingRoomPlayer.Role,
                Team = (MatchTeam)waitingRoomPlayer.Team
            };

            CurrentPlayerRole = currentPlayer.Role;

            gameManagerService.MatchInitialized += OnMatchInitialized;
            gameManagerService.TimerTick += OnTimerTick;
            gameManagerService.MatchCancelled += OnMatchCancelled;
            gameManagerService.NewPasswordReceived += OnNewPasswordReceived;
            gameManagerService.ClueReceived += OnClueReceived;
            gameManagerService.GuessResult += OnGuessResult;
            gameManagerService.ValidationComplete += OnValidationComplete;  
            gameManagerService.BeginRoundValidation += OnBeginRoundValidation;
            gameManagerService.MatchOver += OnMatchOver;
            

        }

        public async Task InitializeAsync()
        {
            try
            {
                await gameManagerService.SubscribeToMatchAsync(gameCode, currentPlayer.Id);
            }catch (Exception ex)
            {
                HandleConnectionError(ex, "Error al suscribirse al la partida");
            }
        }

        private void OnMatchInitialized(MatchInitStateDTO state)
        {

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    currentWordIndex = 1;
                    currentRoundIndex = 1;

                    var thisPlayer = state.Players.FirstOrDefault(p => p.Id == currentPlayer.Id);
                    if (thisPlayer != null)
                    {
                        CurrentPlayerRole = thisPlayer.Role;
                        TeamName = thisPlayer.Team == MatchTeam.RedTeam ?
                            Properties.Langs.Lang.redTeamText : Properties.Langs.Lang.blueTeamText;

                    }

                    CurrentRoundText = string.Format(Properties.Langs.Lang.currentRoundText, currentRoundIndex);
                    CurrentWordCountText = string.Format(Properties.Langs.Lang.currentWordText, currentWordIndex);
                    TeamPointsText = string.Format(Properties.Langs.Lang.teamPointsText, 0);

                    IsLoading = false;
                }
                catch (Exception ex)
                {
                    HandleConnectionError(ex, "Error al inicializar el estado de la partida");
                }
            });
        }

        private void OnTimerTick(int secondsLeft)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                TimerSeconds = secondsLeft;
            });
        }

        private void OnMatchCancelled(string reason)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                windowService.ShowPopUp("Partida Cancelada", reason, PopUpIcon.Warning);
                gameManagerService.Cleanup();
                windowService.GoBack();
            });
        }

        private void OnNewPasswordReceived(PasswordWordDTO password)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentPasswordWord = password.SpanishWord;
                CurrentClue = "Esperando pista...";
                CurrentWordCountText = string.Format(Properties.Langs.Lang.currentWordText, currentWordIndex);
            });
        }

        private void OnClueReceived(string clue)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentClue = clue;
            });
        }

        private void OnGuessResult(GuessResultDTO result)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (result.Team == currentPlayer.Team)
                {
                    TeamPointsText = string.Format(Properties.Langs.Lang.teamPointsText, result.NewScore);
                }

                if (result.IsCorrect && result.Team == currentPlayer.Team)
                {
                    currentWordIndex++;
                    if (currentWordIndex > MaxWordsPerRound)
                    {
                        currentWordIndex = MaxWordsPerRound;
                    }
                    CurrentWordCountText = string.Format(Properties.Langs.Lang.currentWordText, currentWordIndex);
                }

            });
        }

        private void OnBeginRoundValidation(TurnHistoryDTO[] turns)
        {
            // TODO: Nivel 4 - Mostrar la vista de validación
        }

        private void OnValidationComplete(ValidationResultDTO result)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                int myNewScore = (currentPlayer.Team == MatchTeam.RedTeam) ? result.NewRedTeamScore : result.NewBlueTeamScore;
                TeamPointsText = string.Format(Properties.Langs.Lang.teamPointsText, myNewScore);

                if (result.TeamThatWasValidated == MatchTeam.BlueTeam)
                {
                    currentRoundIndex++;
                }

                currentWordIndex = 1;

                CurrentRoundText = string.Format(Properties.Langs.Lang.currentRoundText, currentRoundIndex);
                CurrentWordCountText = string.Format(Properties.Langs.Lang.currentWordText, currentWordIndex);
            });
        }

        private void OnMatchOver(MatchSummaryDTO summary)
        {
            // TODO: Nivel 5 - Navegar a la página de ganadores/perdedores
        }

        private void HandleConnectionError(Exception ex, string customMessage)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                string title = Properties.Langs.Lang.errorTitleText;
                string message = $"{customMessage}\n{Properties.Langs.Lang.unexpectedErrorText}";

                if (ex is TimeoutException)
                {
                    title = Properties.Langs.Lang.timeLimitTitleText;
                    message = Properties.Langs.Lang.serverTimeoutText;
                }
                else if (ex is EndpointNotFoundException)
                {
                    title = Properties.Langs.Lang.connectionErrorTitleText;
                    message = Properties.Langs.Lang.serverConnectionInternetErrorText;
                }
                else if (ex is CommunicationException)
                {
                    title = Properties.Langs.Lang.networkErrorTitleText;
                    message = Properties.Langs.Lang.serverCommunicationErrorText;
                }

                windowService.ShowPopUp(title, message, PopUpIcon.Error);
                gameManagerService.Cleanup();
                windowService.GoBack();
            });
        }
    }
}
