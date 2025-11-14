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
using System.Windows.Controls;
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

        private int timerSeconds = 180; //Cambiado para pruebas
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

        private string currentClue = "Esperando pista..."; //waitingAClueText
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
        public string CurrentGuessText 
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

        private bool canSendGuess = false;
        public bool CanSendGuess
        {
            get => canSendGuess;
            set => SetProperty(ref canSendGuess, value);
        }

        private bool canPassTurn = true;  
        public bool CanPassTurn
        {
            get => canPassTurn;
            set => SetProperty(ref canPassTurn, value);
        }
        private bool canRequestHint = true; 
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
                HandleConnectionError(ex, "Error al suscribirse al la partida"); //errorSubscribingGameText
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
                    HandleConnectionError(ex, "Error al inicializar la partida."); //errorInitializingGameText
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

                if (password.EnglishWord == "END" || password.SpanishWord == "END")
                {
                    // --- INICIO DE LA CORRECCIÓN ---
                    string roundOverMsg = "¡Ronda terminada!";
                    CurrentPasswordWord = roundOverMsg; // Para el Pistero
                    CurrentClue = roundOverMsg; // Para el Adivinador

                    CanSendClue = false;
                    CanSendGuess = false;
                    CanPassTurn = false;    // <-- AÑADIDO
                    CanRequestHint = false; // <-- AÑADIDO

                    IsHintVisible = false;
                    CurrentHintText = string.Empty;
                    CurrentClueText = string.Empty;  // <-- AÑADIDO (Recomendado)
                    CurrentGuessText = string.Empty; // <-- AÑADIDO (Recomendado)
                    return;
                }

                if (CurrentPlayerRole == PlayerRole.ClueGuy)
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
                if (currentPasswordDto == null || currentPasswordDto.EnglishWord == "END" ||
                    currentPasswordDto.SpanishWord == "END")
                {
                    return;
                }
                CurrentClue = clue;
                CanSendGuess = true;
            });
        }

        private void OnGuessResult(GuessResultDTO result)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (result.Team == currentPlayer.Team)
                {
                    if (result.IsCorrect)
                    {
                        ShowSnackbar(Properties.Langs.Lang.correctWordGuessedText);
                    }
                    else if(CurrentPlayerRole == PlayerRole.Guesser)
                    {
                        ShowSnackbar(Properties.Langs.Lang.incorrectWordGuessedText);
                    }
                }
                // Actualizar puntaje de mi equipo
                if (result.Team == currentPlayer.Team)
                {
                    TeamPointsText = string.Format(Properties.Langs.Lang.teamPointsText, result.NewScore);

                    // Si mi equipo acertó, actualizo contador de palabra
                    if (result.IsCorrect)
                    {
                        currentWordIndex++; 
                        if (currentWordIndex > MaxWordsPerRound)
                        {
                            currentWordIndex = MaxWordsPerRound;
                        }
                        CurrentWordCountText = string.Format(Properties.Langs.Lang.currentWordText, currentWordIndex);
                        CurrentGuessText = string.Empty; // Limpiar la caja de texto del Adivinador
                    }
                }

                // 3. Lógica de Habilitar/Deshabilitar Botones (Solo para MI EQUIPO)
                if (result.Team == currentPlayer.Team)
                {
                    if (result.IsCorrect)
                    {
                        // Correcto: Deshabilitar a mi equipo.
                        CanSendClue = false;
                        CanSendGuess = false;
                    }
                    else
                    {
                        // Incorrecto: Re-habilitar al Pistero de mi equipo.
                        CanSendClue = true;
                        CanSendGuess = false;
                    }
                }
            });
        }

        private void OnBeginRoundValidation(TurnHistoryDTO[] turns)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                windowService.NavigateToValidationPage(turns, gameCode, currentPlayer.Id, currentLanguage);

            });
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                

                // 2. Determinar si este jugador ganó
                bool didIWin = summary.WinnerTeam.HasValue && summary.WinnerTeam.Value == currentPlayer.Team;
                var gameEndViewModel = new GameEndViewModel(summary.RedScore, summary.BlueScore, windowService);
                Page endPage;

                if (didIWin)
                {
                    endPage = new WinnersPage();
                }
                else
                {
                    endPage = new LosersPage();
                }
                endPage.DataContext = gameEndViewModel;
                windowService.NavigateTo(endPage);

                gameManagerService.Cleanup();
            });
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

                if (CurrentPlayerRole == PlayerRole.ClueGuy && currentPasswordDto != null)
                {
                    if (currentLanguage.StartsWith("es"))
                    {
                        CurrentPasswordWord = currentPasswordDto.SpanishWord;
                    }
                    else
                    {
                        CurrentPasswordWord = currentPasswordDto.EnglishWord;
                    }
                }
                else if (CurrentPlayerRole == PlayerRole.Guesser)
                {
                    // Si soy el adivinador, oculto la palabra.
                    CurrentPasswordWord = "...";
                }
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
