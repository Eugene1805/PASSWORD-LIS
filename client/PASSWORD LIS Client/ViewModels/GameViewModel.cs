using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.GameManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

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

        private int timerSeconds = 300; //Cambiado para pruebas
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

        private string currentClueText;
        public string CurrentClueText
        {
            get => currentClueText;
            set
            {
                SetProperty(ref currentClueText, value);
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
        private string currentGuessText;
        public string CurrentGuessText // Para el Adivinador
        {
            get => currentGuessText;
            set
            {
                SetProperty(ref currentGuessText, value);
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
        private bool canSendClue = true;
        public bool CanSendClue
        {
            get => canSendClue;
            set => SetProperty(ref canSendClue, value);
        }

        private bool canSendGuess = false; // Adivinador
        public bool CanSendGuess
        {
            get => canSendGuess;
            set => SetProperty(ref canSendGuess, value);
        }

        private bool canPassTurn = true; // Pistero 
        public bool CanPassTurn
        {
            get => canPassTurn;
            set => SetProperty(ref canPassTurn, value);
        }
        private bool canRequestHint = true; // Adivinador 
        public bool CanRequestHint
        {
            get => canRequestHint;
            set => SetProperty(ref canRequestHint, value);
        }

        private string snackbarMessage;
        public string SnackbarMessage
        {
            get => snackbarMessage;
            set => SetProperty(ref snackbarMessage, value);
        }

        private bool isSnackbarVisible;
        public bool IsSnackbarVisible
        {
            get => isSnackbarVisible;
            set => SetProperty(ref isSnackbarVisible, value);
        }

        private string currentHintText;
        public string CurrentHintText
        {
            get => currentHintText;
            set => SetProperty(ref currentHintText, value);
        }

        private bool isHintVisible;
        public bool IsHintVisible
        {
            get => isHintVisible;
            set => SetProperty(ref isHintVisible, value);
        }
        public ICommand SubmitClueCommand { get; }
        public ICommand SubmitGuessCommand { get; }
        public ICommand PassTurnCommand { get; }
        public ICommand RequestHintCommand { get; }

        private readonly IGameManagerService gameManagerService;
        private readonly IWindowService windowService;
        private readonly string gameCode;
        private readonly PlayerDTO currentPlayer;

        private int currentWordIndex = 1; // Contador de palabras (1 a 5)
        private int currentRoundIndex = 1; // Contador de rondas (1 a 5)
        private const int MaxRounds = 5;  // Rondas totales
        private const int MaxWordsPerRound = 5; // Palabras por ronda
        private readonly string currentLanguage;
        private PasswordWordDTO currentPasswordDto;
        public GameViewModel(IGameManagerService gameManagerService, IWindowService windowService, string gameCode, WaitingRoomManagerServiceReference.PlayerDTO waitingRoomPlayer)
        {
            this.gameManagerService = gameManagerService;
            this.windowService = windowService;
            this.gameCode = gameCode;
            currentLanguage = Properties.Settings.Default.languageCode;

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
            gameManagerService.NewRoundStarted += OnNewRoundStarted;
            gameManagerService.ValidationTimerTick += OnValidationTimerTick;

            SubmitClueCommand = new RelayCommand(async (_) => await SendClueAsync(), (_) => CanSendClue && !string.IsNullOrWhiteSpace(CurrentClueText));
            SubmitGuessCommand = new RelayCommand(async (_) => await SendGuessAsync(), (_) => CanSendGuess && !string.IsNullOrWhiteSpace(CurrentGuessText));
            PassTurnCommand = new RelayCommand(async (_) => await PassTurnAsync(), (_) => CanPassTurn);
            RequestHintCommand = new RelayCommand((_) => RequestHint());
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

                    IsLoading = false; // ¡Se desbloquea la UI!
                }
                catch (Exception ex)
                {
                    HandleConnectionError(ex, "Error al inicializar la partida.");
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
                currentPasswordDto = password;

                if (currentPlayer.Role == PlayerRole.ClueGuy)
                {
                    if (currentLanguage.StartsWith("es"))
                    {
                        CurrentPasswordWord = password.SpanishWord;
                    }
                    else
                    {
                        CurrentPasswordWord = password.EnglishWord;
                    }
                }

                CurrentClue = "Esperando pista...";
                CanSendClue = true;
                CanSendGuess = false;
                CurrentWordCountText = string.Format(Properties.Langs.Lang.currentWordText, currentWordIndex);
                IsHintVisible = false;
                CurrentHintText = string.Empty;
            });
        }

        private void OnClueReceived(string clue)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                //Mostrar la pista al Adivinador 
                CurrentClue = clue;
                CanSendGuess = true; // El Adivinador ahora puede adivinar
            });
        }

        private void OnGuessResult(GuessResultDTO result)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (currentPlayer.Role == PlayerRole.Guesser)
                {
                    if (result.IsCorrect)
                    {
                        ShowSnackbar(Properties.Langs.Lang.correctWordGuessedText);
                    }
                    else
                    {
                        ShowSnackbar(Properties.Langs.Lang.incorrectWordGuessedText);
                    }
                }
                // Actualizar puntaje de mi equipo
                if (result.Team == currentPlayer.Team)
                {
                    TeamPointsText = string.Format(Properties.Langs.Lang.teamPointsText, result.NewScore);

                    // --- Petición 2: Arreglo del Contador ---
                    // Si *mi equipo* acertó, actualizo *mi* contador de palabra
                    if (result.IsCorrect)
                    {
                        currentWordIndex++; // <-- Aumenta el contador de palabra
                        if (currentWordIndex > MaxWordsPerRound)
                        {
                            currentWordIndex = MaxWordsPerRound;
                        }
                        CurrentWordCountText = string.Format(Properties.Langs.Lang.currentWordText, currentWordIndex);
                        CurrentGuessText = string.Empty; // Limpiar la caja de texto del Adivinador
                    }
                }

                // Si CUALQUIER equipo acierta, el Pistero puede enviar una nueva pista
                // (Porque la palabra cambia para él)
                if (result.IsCorrect)
                {
                    // Correcto: Deshabilitar a ambos. Esperarán el OnNewPassword del servidor.
                    CanSendClue = false;
                    CanSendGuess = false;
                }
                else
                {
                    // Incorrecto: El Pistero (ClueGuy) debe dar OTRA pista.
                    CanSendClue = true;
                    // El Adivinador (Guesser) debe esperar esa nueva pista.
                    CanSendGuess = false;
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

                // Habilitar botones para la nueva ronda
                CanPassTurn = true;
                CanRequestHint = true;
            });
        }

        private void OnMatchOver(MatchSummaryDTO summary)
        {
            // TODO: Nivel 5 - Navegar a la página de ganadores/perdedores
        }

        private void OnNewRoundStarted(RoundStartStateDTO state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Esta es la lógica para actualizar los roles (Pistero <-> Adivinador)
                var thisPlayer = state.PlayersWithNewRoles.FirstOrDefault(p => p.Id == currentPlayer.Id);
                if (thisPlayer != null)
                {
                    CurrentPlayerRole = thisPlayer.Role;
                }
            });
        }

        private void OnValidationTimerTick(int secondsLeft)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Aquí puedes actualizar un contador de tiempo en la UI de Validación
                // (Si no tienes uno, puedes dejar este método vacío por ahora)
            });
        }

        // --- Petición 4: Lógica para Enviar Pista ---
        private async Task SendClueAsync()
        {
            CanSendClue = false; // Deshabilitar el botón/textbox
            try
            {
                // Enviamos la pista al servidor
                await gameManagerService.SubmitClueAsync(gameCode, currentPlayer.Id, currentClueText);
                CurrentClueText = string.Empty; // Limpiar la caja de texto
            }
            catch (Exception ex)
            {
                HandleConnectionError(ex, "Error al enviar la pista.");
                CanSendClue = true; // Rehabilitar si falló
            }
        }
        private async Task SendGuessAsync()
        {
            CanSendGuess = false; // Deshabilitar hasta que haya nueva pista
            try
            {
                await gameManagerService.SubmitGuessAsync(gameCode, currentPlayer.Id, currentGuessText);
                // No limpiamos el texto, OnGuessResult(true) lo hará si es correcto
            }
            catch (Exception ex)
            {
                HandleConnectionError(ex, "Error al enviar la adivinanza.");
                CanSendGuess = true;
            }
        }
        private async Task PassTurnAsync()
        {
            CanPassTurn = false; // Solo se puede pasar una vez por ronda
            try
            {
                await gameManagerService.PassTurnAsync(gameCode, currentPlayer.Id);
            }
            catch (Exception ex)
            {
                HandleConnectionError(ex, "Error al pasar turno.");
                CanPassTurn = true; // Rehabilitar si falló
            }
        }
        private void RequestHint()
        {
            CanRequestHint = false; // Deshabilitar el botón

            if (currentPasswordDto == null ||
                (string.IsNullOrEmpty(currentPasswordDto.SpanishDescription) && string.IsNullOrEmpty(currentPasswordDto.EnglishDescription)))
            {
                ShowSnackbar(Properties.Langs.Lang.noClueAvailableText);
                CanRequestHint = true; // Permitir que lo intente de nuevo si no había
                return;
            }

            string description = currentLanguage.StartsWith("es") ?
                currentPasswordDto.SpanishDescription : currentPasswordDto.EnglishDescription;

            CurrentHintText = description;
            IsHintVisible = true;
        }

        private async void ShowSnackbar(string message)
        {
            SnackbarMessage = message;
            IsSnackbarVisible = true;
            await Task.Delay(3000); // Muestra el mensaje por 3 segundos
            IsSnackbarVisible = false;
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
