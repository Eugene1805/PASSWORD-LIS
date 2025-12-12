using log4net;
using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.GameManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public class RoundValidationViewModel : BaseViewModel
    {
        private readonly IGameManagerService gameManagerService;
        private readonly string gameCode;
        private readonly int playerId;
        private readonly ILog log = LogManager.GetLogger(typeof(RoundValidationViewModel));
        private readonly DispatcherTimer serverGuardian;
        private readonly Stopwatch serverPulseWatch = new Stopwatch();

        private bool isMatchEnding = false;
        private bool validationReceived = false;

        private const int DefaultValidationSeconds = 30;
        private const int ServerCheckIntervalSeconds = 2;
        private const int ServerTimeoutThresholdSeconds = 6;
        private const string EndGameMarker = "END";

        private int validationSeconds = DefaultValidationSeconds; 
        public int ValidationSeconds
        {
            get => validationSeconds;
            set => SetProperty(ref validationSeconds, value);
        }

        private bool canSubmit = true;
        public bool CanSubmit
        {
            get => canSubmit;
            set => SetProperty(ref canSubmit, value);
        }

        private bool isListEmpty;
        public bool IsListEmpty
        {
            get => isListEmpty;
            set => SetProperty(ref isListEmpty, value);
        }

        private string emptyListMessage;
        public string EmptyListMessage
        {
            get => emptyListMessage;
            set => SetProperty(ref emptyListMessage, value);
        }
        public ObservableCollection<ValidationTurnViewModel> TurnsToValidate 
        { 
            get; 
            private set; 
        }
        public ICommand SubmitVotesCommand 
        { 
            get; 
        }
        public RoundValidationViewModel(List<TurnHistoryDTO> Turns, IGameManagerService GameManagerService,
            IWindowService WindowService,string GameCode, int PlayerId, string Language) : base(WindowService)
        {
            this.gameManagerService = GameManagerService;
            this.gameCode = GameCode;
            this.playerId = PlayerId;

            serverGuardian = CreateServerGuardian();
            StartServerMonitoring();
            SubscribeToServiceEvents();
            InitializeValidationList(Turns, Language);

            SubmitVotesCommand = new RelayCommand(async (_) => await SubmitVotesAsync(), (_) => CanSubmit);
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
        private void StartServerMonitoring()
        {
            serverPulseWatch.Restart();
            serverGuardian.Start();
        }
        private void SubscribeToServiceEvents()
        {
            gameManagerService.ValidationTimerTick += OnValidationTimerTick;
            gameManagerService.ValidationComplete += OnValidationComplete;
            gameManagerService.MatchCancelled += OnMatchCancelled;
            gameManagerService.MatchOver += OnMatchOver;

            if (gameManagerService is ICommunicationObject clientChannel)
            {
                clientChannel.Faulted += OnServerConnectionLost;
                clientChannel.Closed += OnServerConnectionLost;
            }
        }

        private void InitializeValidationList(List<TurnHistoryDTO> turns, string language)
        {
            if (turns == null || turns.Count == 0)
            {
                IsListEmpty = true;
                EmptyListMessage = Properties.Langs.Lang.opposingTeamNotSendCluesText;
                TurnsToValidate = new ObservableCollection<ValidationTurnViewModel>();
            }
            else
            {
                IsListEmpty = false;
                EmptyListMessage = string.Empty;
                var groupedTurns = turns.Where(
                    turn => turn.Password.EnglishWord != EndGameMarker && turn.Password.SpanishWord != EndGameMarker)
                    .GroupBy(turn => turn.TurnId)
                    .Select(group => new ValidationTurnViewModel(group, language));
                TurnsToValidate = new ObservableCollection<ValidationTurnViewModel>(groupedTurns);
            }
        }

        private void OnValidationTimerTick(int secondsLeft)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ValidationSeconds = secondsLeft;
                serverPulseWatch.Restart();
                if (!serverGuardian.IsEnabled)
                {
                    serverGuardian.Start();
                }
            });
        }

        private void OnValidationComplete(ValidationResultDTO result)
        {
            try
            {
                validationReceived = true;
                log.InfoFormat("OnValidationComplete processing - Red: {0}, Blue: {1}",
                    result.NewRedTeamScore, result.NewBlueTeamScore);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    serverGuardian?.Stop();
                    log.InfoFormat("Dispatcher invoked - starting StopServiceMonitoring");
                    StopServiceMonitoring(); 
                    log.InfoFormat("StopServiceMonitoring completed - calling GoBack");
                    windowService.GoBack();
                });
            }
            catch (Exception ex)
            {
                log.Error($"Error in OnValidationComplete: {ex.Message}", ex);
            }
        }

        private void OnMatchCancelled(string reason)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                serverGuardian?.Stop();
                windowService.ShowPopUp(Properties.Langs.Lang.matchCancelledText,
                    reason, PopUpIcon.Warning);
                StopServiceMonitoring();
                gameManagerService.Cleanup();
                windowService.GoToLobby();
            });
        }
        private void OnMatchOver(MatchSummaryDTO summary)
        {
            isMatchEnding = true;
        }

        private async Task SubmitVotesAsync()
        {
            if (isMatchEnding)
            {
                return;
            }
            CanSubmit = false;
            List<ValidationVoteDTO> votes = BuildValidationVotes();

            await ExecuteAsync(async () =>
            {
                try
                {
                    log.InfoFormat("Submitting votes for game {0}...", gameCode);
                    await gameManagerService.SubmitValidationVotesAsync(gameCode, playerId, votes);
                    log.InfoFormat("SubmitValidationVotesAsync completed successfully");
                }
                catch (FaultException fe)
                {
                    HandleFaultException(fe);
                }
                catch (CommunicationException ce)
                {
                    HandleCommunicationException(ce);
                }
                catch (Exception ex)
                {
                    HandleGenericException(ex);
                }
            });
        }

        private List<ValidationVoteDTO> BuildValidationVotes()
        {
            List<ValidationVoteDTO> votes = new List<ValidationVoteDTO>();
            foreach (var turn in TurnsToValidate)
            {
                votes.Add(new ValidationVoteDTO
                {
                    TurnId = turn.TurnId,
                    PenalizeMultiword = turn.PenalizeMultiword,
                    PenalizeSynonym = turn.PenalizeSynonym
                });
            }
            return votes;
        }

        private void HandleFaultException(FaultException fe)
        {
            log.Error($"FaultException in SubmitVotesAsync: {fe.Message}", fe);
            HandleConnectionError(fe, Properties.Langs.Lang.errorSendingVotesText);
            CanSubmit = true;
        }

        private void HandleCommunicationException(CommunicationException ce)
        {
            if (ShouldIgnoreException())
            {
                log.Info("CommunicationException was ignored because validation was already received or match is ending.");
                return;
            }

            log.Error($"CommunicationException in SubmitVotesAsync: {ce.Message}", ce);
            HandleConnectionError(ce, Properties.Langs.Lang.communicationErrorSendingVotesText);
            CanSubmit = true;
        }

        private void HandleGenericException(Exception ex)
        {
            if (ShouldIgnoreException())
            {
                log.Info("Generic Exception was ignored because validation was already received.");
                return;
            }
            log.Error($"Unexpected error in SubmitVotesAsync: {ex.Message}", ex);
            HandleConnectionError(ex, Properties.Langs.Lang.errorSendingVotesText);
            CanSubmit = true;
        }

        private bool ShouldIgnoreException()
        {
            return isMatchEnding || validationReceived;
        }

        private void StopServiceMonitoring()
        {
            try
            {
                serverGuardian?.Stop();
                log.Info("Starting StopServiceMonitoring - unsubscribing from events");

                gameManagerService.ValidationTimerTick -= OnValidationTimerTick;
                gameManagerService.ValidationComplete -= OnValidationComplete;
                gameManagerService.MatchCancelled -= OnMatchCancelled;
                gameManagerService.MatchOver -= OnMatchOver;

                if (gameManagerService is ICommunicationObject clientChannel)
                {
                    clientChannel.Faulted -= OnServerConnectionLost;
                    clientChannel.Closed -= OnServerConnectionLost;
                }

                log.Info("StopServiceMonitoring completed successfully");
            }
            catch (Exception ex)
            {
                log.Error($"Error during StopServiceMonitoring: {ex.Message}", ex);
            }
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
                    Properties.Langs.Lang.connectionLostValidationText,
                    PopUpIcon.Error);

                StopServiceMonitoring();
                App.ResetServices();
                ForceReturnToLogin();
            });
        }

        private void CheckServerPulse(object sender, EventArgs e)
        {
            if (serverPulseWatch.IsRunning && serverPulseWatch.Elapsed.TotalSeconds > ServerTimeoutThresholdSeconds)
            {
                serverGuardian.Stop();
                OnServerConnectionLost(this, EventArgs.Empty);
            }
        }

        private void HandleConnectionError(Exception exception, string customMessage)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                string title = Properties.Langs.Lang.errorTitleText;
                string message = $"{customMessage}\n{Properties.Langs.Lang.unexpectedErrorText}";

                serverGuardian?.Stop();

                if (exception is TimeoutException)
                {
                    title = Properties.Langs.Lang.timeLimitTitleText;
                    message = Properties.Langs.Lang.serverTimeoutText;
                }   
                else if (exception is EndpointNotFoundException)
                {
                    title = Properties.Langs.Lang.connectionErrorTitleText;
                    message = Properties.Langs.Lang.serverConnectionInternetErrorText;
                }
                else if (exception is CommunicationException)
                {
                    title = Properties.Langs.Lang.networkErrorTitleText;
                    message = Properties.Langs.Lang.serverCommunicationErrorText;
                }

                windowService.ShowPopUp(title, message, PopUpIcon.Error);
                StopServiceMonitoring();
                gameManagerService.Cleanup();

                ForceReturnToLogin();
            });
        }

        private void ForceReturnToLogin()
        {
            serverGuardian?.Stop();
            StopServiceMonitoring();

            App.ResetServices();

            Application.Current.Dispatcher.Invoke(() =>
            {
                windowService.ReturnToLogin();
            });
        }
    }
}
