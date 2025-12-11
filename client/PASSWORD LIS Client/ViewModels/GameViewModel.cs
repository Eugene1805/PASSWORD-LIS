using log4net;
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
using System.Windows.Threading;
using System.Diagnostics;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class GameViewModel : BaseViewModel
    {
        private enum CancellationReason
        {
            Unknown,
            PlayerDisconnected,
            HostLeft,
            ServerError,
            ConnectionLost
        }

        private readonly IGameManagerService gameManagerService;
        private readonly string gameCode;
        private readonly PlayerDTO currentPlayer;
        private readonly ILog log = LogManager.GetLogger(typeof(GameViewModel));
        private readonly DispatcherTimer serverGuardian;
        private readonly Stopwatch serverPulseWatch = new Stopwatch();
        private readonly string currentLanguage;

        private static readonly Dictionary<string, CancellationReason> CancellationReasonPatterns = new Dictionary<string, CancellationReason>
        {
            { "PLAYER_DISCONNECTED", CancellationReason.PlayerDisconnected },
            { "PLAYER_LEFT", CancellationReason.PlayerDisconnected },
            { "HAS DISCONNECTED", CancellationReason.PlayerDisconnected },
            { "HOST_LEFT", CancellationReason.HostLeft },
            { "HOST_DISCONNECTED", CancellationReason.HostLeft },
            { "SERVER_ERROR", CancellationReason.ServerError },
            { "INTERNAL_ERROR", CancellationReason.ServerError },
            { "INTERNAL SERVER ERROR", CancellationReason.ServerError },
            { "NOT ENOUGH WORDS", CancellationReason.ServerError },
            { "TIMEOUT", CancellationReason.ConnectionLost },
            { "CONNECTION_LOST", CancellationReason.ConnectionLost }
        };

        private int currentWordIndex = 1;
        private PasswordWordDTO currentPasswordDto;
        private bool isSuddenDeathActive = false;
        private bool isMatchEnding = false;

        private const int MaximumWordsPerRound = 5;
        private const int DefaultTimerSeconds = 60;
        private const int ServerCheckIntervalSeconds = 10;
        private const int ServerTimeoutThresholdSeconds = 6;
        private const int SnackbarDurationMilliseconds = 3000;
        private const int InitialGameScore = 0;
        private const string EndGameMarker = "END";
        private const string WaitingPlaceholder = "...";
        private const string SpanishLanguagePrefix = "es";
        private const string PassTurnKeywordEnglish = "passed";
        private const string PassTurnKeywordSpanish = "pasó";

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

        private int timerSeconds = DefaultTimerSeconds;
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

        private string currentPasswordWord = WaitingPlaceholder;
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


        public GameViewModel(IGameManagerService gameManagerService, IWindowService windowService, 
            string gameCode, WaitingRoomManagerServiceReference.PlayerDTO waitingRoomPlayer) : base(windowService)
        {
            this.gameManagerService = gameManagerService;
            this.gameCode = gameCode;
            currentLanguage = Properties.Settings.Default.languageCode;

            currentPlayer = InitializePlayer(waitingRoomPlayer);
            CurrentPlayerRole = currentPlayer.Role;

            serverGuardian = CreateServerGuardian();

            SubscribeToGameEvents();

            SubmitClueCommand = new RelayCommand(async (_) => await SendClueAsync(), 
                (_) => CanSendClue && !string.IsNullOrWhiteSpace(CurrentClueText));
            SubmitGuessCommand = new RelayCommand(async (_) => await SendGuessAsync(), 
                (_) => CanSendGuess && !string.IsNullOrWhiteSpace(CurrentGuessText));
            PassTurnCommand = new RelayCommand(async (_) => await PassTurnAsync(), (_) => CanPassTurn);
            RequestHintCommand = new RelayCommand((_) => RequestHint());
        }

        public async Task InitializeAsync()
        {
            await ExecuteAsync(async () =>
            {
                try
                {
                    if (gameManagerService is ICommunicationObject clientChannel)
                    {
                        clientChannel.Faulted += OnServerConnectionLost;
                        clientChannel.Closed += OnServerConnectionLost;
                    }
                    await gameManagerService.SubscribeToMatchAsync(gameCode, currentPlayer.Id);
                }
                catch (Exception ex)
                {
                    HandleConnectionError(ex, Properties.Langs.Lang.errorSubscribingGameText);
                }
            });
        }

        private PlayerDTO InitializePlayer(WaitingRoomManagerServiceReference.PlayerDTO waitingRoomPlayer)
        {
            return new PlayerDTO
            {
                Id = waitingRoomPlayer.Id,
                Nickname = waitingRoomPlayer.Nickname,
                PhotoId = waitingRoomPlayer.PhotoId,
                Role = (PlayerRole)waitingRoomPlayer.Role,
                Team = (MatchTeam)waitingRoomPlayer.Team
            };
        }
        private DispatcherTimer CreateServerGuardian()
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(ServerCheckIntervalSeconds)
            };
            timer.Tick += CheckServerPulse;
            return timer;
        }

        private void SubscribeToGameEvents()
        {
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
        }

        private void OnMatchInitialized(MatchInitStateDTO state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    serverPulseWatch.Restart();
                    serverGuardian.Start();

                    currentWordIndex = 1;
                    var currentRoundIndex = 1;

                    var thisPlayer = state.Players.FirstOrDefault(player => player.Id == currentPlayer.Id);
                    if (thisPlayer != null)
                    {
                        CurrentPlayerRole = thisPlayer.Role;
                        TeamName = thisPlayer.Team == MatchTeam.RedTeam ?
                            Properties.Langs.Lang.redTeamText : Properties.Langs.Lang.blueTeamText;
                    }

                    CurrentRoundText = string.Format(Properties.Langs.Lang.currentRoundText, currentRoundIndex);
                    CurrentWordCountText = string.Format(Properties.Langs.Lang.currentWordText, currentWordIndex);
                    TeamPointsText = string.Format(Properties.Langs.Lang.teamPointsText, InitialGameScore);

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
                serverPulseWatch.Restart();
                if (!serverGuardian.IsEnabled)
                {
                    serverGuardian.Start();
                }
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
                isMatchEnding = true;

                log.WarnFormat("Match cancelled. Server reason: {0}", reason);

                string localizedMessage = GetLocalizedCancellationMessage(reason);

                windowService.ShowPopUp(Properties.Langs.Lang.matchCancelledText, localizedMessage, PopUpIcon.Warning);
                ForceReturnToLogin();
            });
        }

        private string GetLocalizedCancellationMessage(string serverReason)
        {
            if (string.IsNullOrEmpty(serverReason))
            {
                return Properties.Langs.Lang.unexpectedErrorText;
            }

            var reasonKey = FindMatchingReasonKey(serverReason.ToUpperInvariant());
            return GetMessageForReasonKey(reasonKey, serverReason);
        }

        private static CancellationReason FindMatchingReasonKey(string reasonUpper)
        {
            return CancellationReasonPatterns
                .FirstOrDefault(pattern => reasonUpper.Contains(pattern.Key))
                .Value;
        }

        private string GetMessageForReasonKey(CancellationReason reasonKey, string originalReason)
        {
            switch (reasonKey)
            {
                case CancellationReason.PlayerDisconnected:
                    return Properties.Langs.Lang.playerDisconnectedText;
                case CancellationReason.HostLeft:
                    return Properties.Langs.Lang.hostLeftText;
                case CancellationReason.ServerError:
                    return Properties.Langs.Lang.unexpectedServerErrorText;
                case CancellationReason.ConnectionLost:
                    return Properties.Langs.Lang.connectionUnexpected;
                default:
                    log.WarnFormat("Unknown cancellation reason received: {0}", originalReason);
                    return Properties.Langs.Lang.unexpectedErrorText;
            }
        }

        private void OnNewPasswordReceived(PasswordWordDTO password)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                currentPasswordDto = password;

                bool isRoundEnded = password.EnglishWord == EndGameMarker || password.SpanishWord == EndGameMarker;

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
                    CurrentPasswordWord = currentLanguage.StartsWith(SpanishLanguagePrefix) 
                    ? password.SpanishWord : password.EnglishWord;
                }
                else
                {
                    CurrentPasswordWord = WaitingPlaceholder;
                }

                CurrentClue = Properties.Langs.Lang.waitingAClueText;
                CanSendClue = true;
                CanSendGuess = false;

                if (isSuddenDeathActive)
                {
                    CurrentWordCountText = string.Empty;
                    CanRequestHint = true;
                }
                else
                {
                    CurrentWordCountText = string.Format(Properties.Langs.Lang.currentWordText, currentWordIndex);
                }

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

                if (currentPasswordDto == null || currentPasswordDto.EnglishWord == EndGameMarker ||
                            currentPasswordDto.SpanishWord == EndGameMarker)
                {
                    return;
                }

                CurrentClue = clue;

                bool isPassMessage = clue.ToLower().Contains(PassTurnKeywordEnglish) 
                || clue.ToLower().Contains(PassTurnKeywordSpanish) ;

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
                if (result.Team != currentPlayer.Team)
                {
                    return;
                }

                if (result.IsCorrect)
                {
                    _ = ShowSnackbar(Properties.Langs.Lang.correctWordGuessedText);
                }
                else if (CurrentPlayerRole == PlayerRole.Guesser)
                {
                    _ = ShowSnackbar(Properties.Langs.Lang.incorrectWordGuessedText);
                }

                TeamPointsText = string.Format(Properties.Langs.Lang.teamPointsText, result.NewScore);

                if (result.IsCorrect)
                {
                    currentWordIndex++;
                    if (currentWordIndex > MaximumWordsPerRound)
                    {
                        currentWordIndex = MaximumWordsPerRound;
                    }
                    CurrentWordCountText = string.Format(Properties.Langs.Lang.currentWordText, currentWordIndex);
                    CurrentGuessText = string.Empty;
                }

                CanSendClue = !result.IsCorrect;
                CanSendGuess = false;
            });
        }

        private void OnBeginRoundValidation(List<TurnHistoryDTO> turns)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                serverGuardian?.Stop();
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
                int myNewScore = (currentPlayer.Team == MatchTeam.RedTeam) 
                ? result.NewRedTeamScore : result.NewBlueTeamScore;
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
                var gameEndViewModel = new GameEndViewModel(summary.RedScore, summary.BlueScore, windowService);
                Page endPage;

                if (summary.WinnerTeam == null)
                {
                    endPage = new DrawPage();
                }
                else
                {
                    bool didIWin = summary.WinnerTeam.Value == currentPlayer.Team;
                    endPage = didIWin ? (Page)new WinnersPage() : new LosersPage();
                }

                endPage.DataContext = gameEndViewModel;
                windowService.NavigateTo(endPage);
                SafeCleanup();
            });
        }

        private void OnNewRoundStarted(RoundStartStateDTO state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                serverPulseWatch.Restart();
                if (!serverGuardian.IsEnabled)
                {
                    serverGuardian.Start();
                }
                var currentRoundIndex = state.CurrentRound;
                CurrentRoundText = string.Format(Properties.Langs.Lang.currentRoundText, currentRoundIndex);

                var thisPlayer = state.PlayersWithNewRoles.FirstOrDefault(p => p.Id == currentPlayer.Id);
                if (thisPlayer != null)
                {
                    CurrentPlayerRole = thisPlayer.Role;
                }

                if (CurrentPlayerRole == PlayerRole.ClueGuy && currentPasswordDto != null)
                {
                    if (currentLanguage.StartsWith(SpanishLanguagePrefix))
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
                    CurrentPasswordWord = WaitingPlaceholder;
                }

                CanPassTurn = !isSuddenDeathActive;
            });
        }
        private void OnSuddenDeathStarted()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                serverPulseWatch.Restart();
                if (!serverGuardian.IsEnabled)
                {
                    serverGuardian.Start();
                }
                isSuddenDeathActive = true;
                IsSuddenDeathVisible = true;
                CanPassTurn = false;

                if (CurrentPlayerRole == PlayerRole.Guesser)
                {
                    CurrentClue = Properties.Langs.Lang.waitingAClueText;
                    CurrentGuessText = string.Empty;
                    CanSendGuess = false;
                }

                CurrentRoundText = Properties.Langs.Lang.suddenDeathRoundText;
                CurrentWordCountText = string.Empty;
                _ = ShowSnackbar(Properties.Langs.Lang.suddenDeathFirstGuessWinsText);
            });
        }

        private async Task SendClueAsync()
        {
            CanSendClue = false;
            await ExecuteAsync(async () =>
            {
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
            });
        }
        private async Task SendGuessAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentClue) ||
                CurrentClue == Properties.Langs.Lang.waitingAClueText ||
                CurrentClue.ToLower().Contains(PassTurnKeywordEnglish) ||
                CurrentClue.ToLower().Contains(PassTurnKeywordSpanish))
            {
                return;
            }

            CanSendGuess = false;

            await ExecuteAsync(async () =>
            {
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
            });
        }
        private async Task PassTurnAsync()
        {
            CanPassTurn = false;
            await ExecuteAsync(async () =>
            {
                try
                {
                    await gameManagerService.PassTurnAsync(gameCode, currentPlayer.Id);
                }
                catch (Exception ex)
                {
                    HandleConnectionError(ex, Properties.Langs.Lang.errorPassingTurnText);
                    CanPassTurn = true;
                }
            });           
        }
        private void RequestHint()
        {
            CanRequestHint = false;

            if (currentPasswordDto == null)
            {
                _ = ShowSnackbar(Properties.Langs.Lang.noClueAvailableText);
                CanRequestHint = true;
                return;
            }

            if (string.IsNullOrEmpty(currentPasswordDto.SpanishDescription) 
                && string.IsNullOrEmpty(currentPasswordDto.EnglishDescription))
            {
                _ = ShowSnackbar(Properties.Langs.Lang.noClueAvailableText);
                CanRequestHint = true;
                return;
            }

            string description = currentLanguage.StartsWith(SpanishLanguagePrefix) ?
                currentPasswordDto.SpanishDescription : currentPasswordDto.EnglishDescription;

            CurrentHintText = description;
            IsHintVisible = true;
        }

        private void OnServerConnectionLost(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (isMatchEnding)
                {
                    return;
                }
                isMatchEnding = true;

                serverGuardian?.Stop();

                windowService.ShowPopUp(
                    Properties.Langs.Lang.connectionErrorTitleText, 
                    Properties.Langs.Lang.unexpectedServerErrorText,
                    PopUpIcon.Error);
                ForceReturnToLogin();
            });
        }
        private void ForceReturnToLogin()
        {
            UnsubscribeFromEvents();
            SafeCleanup();

            App.ResetServices();

            Application.Current.Dispatcher.Invoke(() =>
            {
                windowService.ReturnToLogin();
            });
        }

        private void SafeCleanup()
        {
            try
            {
                if (gameManagerService is ICommunicationObject clientChannel)
                {
                    clientChannel.Faulted -= OnServerConnectionLost;
                    clientChannel.Closed -= OnServerConnectionLost;

                    if (clientChannel.State == CommunicationState.Opened)
                    {
                        gameManagerService.Cleanup();
                    }
                    else
                    {
                        clientChannel.Abort();
                    }
                }
                else
                {
                    gameManagerService.Cleanup();
                }
            }
            catch (Exception ex)
            {
                log.WarnFormat("Error cleaning the connection: {}", ex.Message);

                if (gameManagerService is ICommunicationObject clientChannel)
                {
                    clientChannel.Abort();
                }
            }
        }

        private void CheckServerPulse(object sender, EventArgs e)
        {
            if (serverPulseWatch.IsRunning && serverPulseWatch.Elapsed.TotalSeconds > ServerTimeoutThresholdSeconds)
            {
                serverGuardian.Stop();
                OnServerConnectionLost(this, EventArgs.Empty);
            }
        }

        private async Task ShowSnackbar(string message)
        {
            SnackbarMessage = message;
            IsSnackbarVisible = true;
            await Task.Delay(SnackbarDurationMilliseconds);
            IsSnackbarVisible = false;
        }

        private void UnsubscribeFromEvents()
        {
            serverGuardian?.Stop();
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
                if (isMatchEnding)
                {
                    return;
                }
                isMatchEnding = true;
                serverGuardian?.Stop();
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
                ForceReturnToLogin();
            });
        }
    }
}
