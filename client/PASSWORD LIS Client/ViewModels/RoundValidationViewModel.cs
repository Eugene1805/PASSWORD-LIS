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
        public ObservableCollection<ValidationTurnViewModel> TurnsToValidate { get; }
        private int validationSeconds = 30; 
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

        public ICommand SubmitVotesCommand { get; }

        private readonly IGameManagerService gameManagerService;
        private readonly string gameCode;
        private readonly int playerId;
        private readonly ILog log = LogManager.GetLogger(typeof(RoundValidationViewModel));
        private bool isMatchEnding = false;
        private readonly DispatcherTimer serverGuardian;
        private readonly Stopwatch serverPulseWatch = new Stopwatch();


        public RoundValidationViewModel(List<TurnHistoryDTO> turns, IGameManagerService gameManagerService, IWindowService windowService,
            string gameCode, int playerId, string language) : base(windowService)
        {
            this.gameManagerService = gameManagerService;
            this.gameCode = gameCode;
            this.playerId = playerId;

            serverGuardian = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            serverGuardian.Tick += CheckServerPulse;
            serverPulseWatch.Restart();
            serverGuardian.Start();

            gameManagerService.ValidationTimerTick += OnValidationTimerTick;
            gameManagerService.ValidationComplete += OnValidationComplete;
            gameManagerService.MatchCancelled += OnMatchCancelled;
            gameManagerService.MatchOver += OnMatchOver;

            if (gameManagerService is ICommunicationObject clientChannel)
            {
                clientChannel.Faulted += OnServerConnectionLost;
                clientChannel.Closed += OnServerConnectionLost;
            }

            if (turns == null || turns.Count == 0)
            {
                IsListEmpty = true;
                EmptyListMessage = "El equipo contrario no envió pistas."; //Properties.Langs.Lang.opponentNoCluesText
                TurnsToValidate = new ObservableCollection<ValidationTurnViewModel>();
            }
            else
            {
                IsListEmpty = false;
                EmptyListMessage = string.Empty;
                var groupedTurns = turns.Where(turn => turn.Password.EnglishWord != "END" && turn.Password.SpanishWord != "END")
                    .GroupBy(turn => turn.TurnId)
                    .Select(group => new ValidationTurnViewModel(group, language));
                TurnsToValidate = new ObservableCollection<ValidationTurnViewModel>(groupedTurns);
            }

            SubmitVotesCommand = new RelayCommand(async (_) => await SubmitVotesAsync(), (_) => CanSubmit);
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
                log.InfoFormat("OnValidationComplete processing - Red: {0}, Blue: {1}", result.NewRedTeamScore, result.NewBlueTeamScore);
                LogCurrentState();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    serverGuardian?.Stop();
                    log.InfoFormat("Dispatcher invoked - starting cleanup");
                    Cleanup();
                    log.InfoFormat("Cleanup completed - calling GoBack");
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
                Cleanup();
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
                    log.Error($"FaultException in SubmitVotesAsync: {fe.Message}", fe);
                    HandleConnectionError(fe, "Error al enviar los votos");
                    CanSubmit = true;
                }
                catch (CommunicationException ce)
                {
                    if (isMatchEnding)
                    {
                        log.Info("CommunicationException was ignored because the game has ended (MatchOver received).");
                        return;
                    }

                    log.Error($"CommunicationException in SubmitVotesAsync: {ce.Message}", ce);
                    HandleConnectionError(ce, "Error de comunicación al enviar los votos");
                    CanSubmit = true;
                }
                catch (Exception ex)
                {
                    if (isMatchEnding)
                    {
                        log.Info("Generic Exception was ignored because the game has ended.");
                        return;
                    }
                    log.Error($"Unexpected error in SubmitVotesAsync: {ex.Message}", ex);
                    HandleConnectionError(ex, "Error al enviar los votos");
                    CanSubmit = true;
                }
            });
        }
        private void LogCurrentState()
        {
            try
            {
                log.InfoFormat("Current state - CanSubmit: {0}, TurnsCount: {1}", CanSubmit, TurnsToValidate.Count);
                log.InfoFormat("Application.Current.MainWindow null: {0}", Application.Current?.MainWindow == null);

                if (Application.Current?.MainWindow != null && Application.Current.MainWindow.Content is Frame frame)
                {
                    log.InfoFormat("Frame CanGoBack: {0}, Content: {1}", frame.CanGoBack, frame.Content?.GetType().Name);
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error logging current state: {ex.Message}", ex);
            }
        }
        private void Cleanup()
        {
            try
            {
                serverGuardian?.Stop();
                log.Info("Starting cleanup - unsubscribing from events");
                gameManagerService.ValidationTimerTick -= OnValidationTimerTick;
                gameManagerService.ValidationComplete -= OnValidationComplete;
                gameManagerService.MatchCancelled -= OnMatchCancelled;
                gameManagerService.MatchOver -= OnMatchOver;

                if (gameManagerService is ICommunicationObject clientChannel)
                {
                    clientChannel.Faulted -= OnServerConnectionLost;
                    clientChannel.Closed -= OnServerConnectionLost;
                }

                log.Info("Cleanup completed successfully");
            }
            catch (Exception ex)
            {
                log.Error($"Error during cleanup: {ex.Message}", ex);
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
                    "Conexión perdida durante la validación.",
                    PopUpIcon.Error);

                Cleanup();
                App.ResetServices();
                windowService.GoToLobby();
            });
        }

        private void CheckServerPulse(object sender, EventArgs e)
        {
            if (serverPulseWatch.IsRunning && serverPulseWatch.Elapsed.TotalSeconds > 6)
            {
                serverGuardian.Stop();
                OnServerConnectionLost(this, EventArgs.Empty);
            }
        }

        private void HandleConnectionError(Exception ex, string customMessage)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                string title = Properties.Langs.Lang.errorTitleText;
                string message = $"{customMessage}\n{Properties.Langs.Lang.unexpectedErrorText}";

                serverGuardian?.Stop();

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
                Cleanup();
                gameManagerService.Cleanup();

                windowService.GoToLobby();
            });
        }
    }
}
