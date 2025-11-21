using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.GameManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System;
using System.Collections.Generic;
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
        private readonly IGameManagerService gameManagerService;
        private readonly IWindowService windowService;
        private readonly string gameCode;
        private readonly PlayerDTO currentPlayer;

        private int currentWordIndex = 1;
        private int currentRoundIndex = 1;
        private readonly string currentLanguage;
        private PasswordWordDTO currentPasswordDto;
        private bool isSuddenDeathActive = false;
        private bool isMatchEnding = false;

        private const int MaxWordsPerRound = 5; // Palabras por ronda

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

        private string currentClue = Properties.Langs.Lang.waitingAClueText;
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

        private bool isSuddenDeathVisible;
        public bool IsSuddenDeathVisible
        {
            get => isSuddenDeathVisible;
            set => SetProperty(ref isSuddenDeathVisible, value);
        }

        public ICommand SubmitClueCommand { get; }
        public ICommand SubmitGuessCommand { get; }
        public ICommand PassTurnCommand { get; }
        public ICommand RequestHintCommand { get; }


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
            gameManagerService.SuddenDeathStarted += OnSuddenDeathStarted;

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
                HandleConnectionError(ex, Properties.Langs.Lang.errorSubscribingGameText); 
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

                    var thisPlayer = state.Players.FirstOrDefault(player => player.Id == currentPlayer.Id);
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
                    HandleConnectionError(ex, Properties.Langs.Lang.errorInitializingGameText); 
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
                if (isMatchEnding)
                {
                    return;
                }


                windowService.ShowPopUp(Properties.Langs.Lang.matchCancelledText, reason, PopUpIcon.Warning);
                UnsubscribeFromEvents();
                gameManagerService.Cleanup();

                var lobbyViewModel = new LobbyViewModel(windowService, App.FriendsManagerService, App.WaitRoomManagerService, 
                    App.ReportManagerService);
                var lobbyPage = new LobbyPage { DataContext = lobbyViewModel };
                windowService.NavigateTo(lobbyPage);
            });
        }

        private void OnNewPasswordReceived(PasswordWordDTO password)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                currentPasswordDto = password;

                bool isRoundEnded = password.EnglishWord == "END" || password.SpanishWord == "END" ;

                if (isRoundEnded && !isSuddenDeathActive)
                {
                    string roundOverMsg = Properties.Langs.Lang.roundCompleted;
                    CurrentPasswordWord = roundOverMsg;
                    CurrentClue = roundOverMsg; 

                    CanSendClue = false;
                    CanSendGuess = false;
                    CanPassTurn = false;
                    CanRequestHint = false;

                    IsHintVisible = false;
                    CurrentHintText = string.Empty;
                    CurrentClueText = string.Empty;
                    CurrentGuessText = string.Empty;
                    return;
                }

                if (CurrentPlayerRole == PlayerRole.ClueGuy)
                {
                    CurrentPasswordWord = currentLanguage.StartsWith("es") ? password.SpanishWord : password.EnglishWord;
                }
                else
                {
                    CurrentPasswordWord = "...";
                }

                CurrentClue = Properties.Langs.Lang.waitingAClueText;
                CanSendClue = true;
                CanSendGuess = false;

                CurrentWordCountText = string.Format(Properties.Langs.Lang.currentWordText, currentWordIndex);
                IsHintVisible = false;
                CurrentHintText = string.Empty;
                CurrentGuessText = string.Empty;
            });
        }

        private void OnClueReceived(string clue)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (isSuddenDeathActive)
                {
                    CurrentClue = clue;
                    CanSendGuess = true;
                    return;
                }

                if (currentPasswordDto == null || currentPasswordDto.EnglishWord == "END" ||
                            currentPasswordDto.SpanishWord == "END")
                {
                    return;
                }

                CurrentClue = clue;

                bool isPassMessage = clue.ToLower().Contains("passed") || clue.ToLower().Contains("pasó") ;

                if (isPassMessage)
                {
                    CanSendGuess = false;
                }
                else
                {
                    CanSendGuess = true;
                }
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

                if (result.Team == currentPlayer.Team)
                {
                    TeamPointsText = string.Format(Properties.Langs.Lang.teamPointsText, result.NewScore);

                    if (result.IsCorrect)
                    {
                        currentWordIndex++; 
                        if (currentWordIndex > MaxWordsPerRound)
                        {
                            currentWordIndex = MaxWordsPerRound;
                        }
                        CurrentWordCountText = string.Format(Properties.Langs.Lang.currentWordText, currentWordIndex);
                        CurrentGuessText = string.Empty;
                    }
                }

                if (result.Team == currentPlayer.Team)
                {
                    if (result.IsCorrect)
                    {
                        CanSendClue = false;
                        CanSendGuess = false;
                    }
                    else
                    {
                        CanSendClue = true;
                        CanSendGuess = false;
                    }
                }
            });
        }

        private void OnBeginRoundValidation(List<TurnHistoryDTO> turns)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var validationViewModel = new RoundValidationViewModel(
                turns, gameManagerService, windowService, gameCode, currentPlayer.Id, currentLanguage);
                var validationPage = new RoundValidationPage { DataContext = validationViewModel };

                windowService.NavigateTo(validationPage);

            });
        }

        private void OnValidationComplete(ValidationResultDTO result)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                int myNewScore = (currentPlayer.Team == MatchTeam.RedTeam) ? result.NewRedTeamScore : result.NewBlueTeamScore;
                TeamPointsText = string.Format(Properties.Langs.Lang.teamPointsText, myNewScore);

                currentWordIndex = 1;
                CurrentWordCountText = string.Format(Properties.Langs.Lang.currentWordText, currentWordIndex);

                CanRequestHint = true;
            });
        }

        private void OnMatchOver(MatchSummaryDTO summary)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                isMatchEnding = true;

                UnsubscribeFromEvents();

                bool didIWin = summary.WinnerTeam.HasValue && summary.WinnerTeam.Value == currentPlayer.Team;
                var gameEndViewModel = new GameEndViewModel(summary.RedScore, summary.BlueScore, windowService);
                Page endPage = didIWin ? (Page)new WinnersPage() : new LosersPage();
                endPage.DataContext = gameEndViewModel;
                windowService.NavigateTo(endPage);
                gameManagerService.Cleanup();
            });
        }

        private void OnNewRoundStarted(RoundStartStateDTO state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                currentRoundIndex = state.CurrentRound;
                CurrentRoundText = string.Format(Properties.Langs.Lang.currentRoundText, currentRoundIndex);

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
                    CurrentPasswordWord = "...";
                }

                CanPassTurn = !isSuddenDeathActive;
            });
        }
        private void OnSuddenDeathStarted()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                isSuddenDeathActive = true;
                IsSuddenDeathVisible = true;
                CanPassTurn = false;
                currentPasswordDto = null;

                if (CurrentPlayerRole == PlayerRole.Guesser)
                {
                    CurrentClue = Properties.Langs.Lang.waitingAClueText;
                    CurrentGuessText = string.Empty;
                    CanSendGuess = false;
                }

                ShowSnackbar("¡Muerte Súbita! El primero en acertar gana.");
            });
        }

        private async Task SendClueAsync()
        {
            CanSendClue = false;
            try
            {
                await gameManagerService.SubmitClueAsync(gameCode, currentPlayer.Id, currentClueText);
                CurrentClueText = string.Empty; 
            }
            catch (Exception ex)
            {
                if (isMatchEnding)
                {
                    return;
                }
                HandleConnectionError(ex, Properties.Langs.Lang.errorSendingClueText);
                CanSendClue = true;
            }
        }
        private async Task SendGuessAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentClue) ||
                CurrentClue == Properties.Langs.Lang.waitingAClueText ||
                CurrentClue.ToLower().Contains("passed") ||
                CurrentClue.ToLower().Contains("pasó"))
            {
                return;
            }

            CanSendGuess = false;
            try
            {
                await gameManagerService.SubmitGuessAsync(gameCode, currentPlayer.Id, currentGuessText);
            }
            catch (Exception ex)
            {
                if (isMatchEnding)
                {
                    return;
                }
                HandleConnectionError(ex, Properties.Langs.Lang.errorSendingRiddleText);
                CanSendGuess = true;
            }
        }
        private async Task PassTurnAsync()
        {
            CanPassTurn = false;
            try
            {
                await gameManagerService.PassTurnAsync(gameCode, currentPlayer.Id);
            }
            catch (Exception ex)
            {
                HandleConnectionError(ex, Properties.Langs.Lang.errorPassingTurnText);
                CanPassTurn = true;
            }
        }
        private void RequestHint()
        {
            CanRequestHint = false;

            if (currentPasswordDto == null)
            {
                ShowSnackbar(Properties.Langs.Lang.noClueAvailableText);
                CanRequestHint = true;
                return;
            }

            if (string.IsNullOrEmpty(currentPasswordDto.SpanishDescription) && string.IsNullOrEmpty(currentPasswordDto.EnglishDescription))
            {
                ShowSnackbar(Properties.Langs.Lang.noClueAvailableText);
                CanRequestHint = true;
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
            await Task.Delay(3000); 
            IsSnackbarVisible = false;
        }

        private void UnsubscribeFromEvents()
        {
            gameManagerService.MatchInitialized -= OnMatchInitialized;
            gameManagerService.TimerTick -= OnTimerTick;
            gameManagerService.MatchCancelled -= OnMatchCancelled;
            gameManagerService.NewPasswordReceived -= OnNewPasswordReceived;
            gameManagerService.ClueReceived -= OnClueReceived;
            gameManagerService.GuessResult -= OnGuessResult;
            gameManagerService.ValidationComplete -= OnValidationComplete;
            gameManagerService.BeginRoundValidation -= OnBeginRoundValidation;
            gameManagerService.MatchOver -= OnMatchOver;
            gameManagerService.NewRoundStarted -= OnNewRoundStarted;
            gameManagerService.SuddenDeathStarted -= OnSuddenDeathStarted;
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
                UnsubscribeFromEvents();
                gameManagerService.Cleanup();
                windowService.GoBack();
            });
        }
    }
}
