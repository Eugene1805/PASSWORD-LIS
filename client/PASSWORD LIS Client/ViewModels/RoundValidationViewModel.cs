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
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class RoundValidationViewModel : BaseViewModel
    {
        public ObservableCollection<ValidationTurnViewModel> TurnsToValidate { get; }
        private int validationSeconds = 60; //CAMBIADO PARA PRUEBAS DE 20 A 60
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

        public ICommand SubmitVotesCommand { get; }

        private readonly IGameManagerService gameManagerService;
        private readonly IWindowService windowService;
        private readonly string gameCode;
        private readonly int playerId;
        private readonly string language;
        private readonly ILog log = LogManager.GetLogger(typeof(RoundValidationViewModel));
        private bool isMatchEnding = false;

        public RoundValidationViewModel(List<TurnHistoryDTO> turns, IGameManagerService gameManagerService, IWindowService windowService,
            string gameCode, int playerId, string language)
        {
            this.gameManagerService = gameManagerService;
            this.windowService = windowService;
            this.gameCode = gameCode;
            this.playerId = playerId;
            this.language = language;

            gameManagerService.ValidationTimerTick += OnValidationTimerTick;
            gameManagerService.ValidationComplete += OnValidationComplete;
            gameManagerService.MatchCancelled += OnMatchCancelled;
            gameManagerService.MatchOver += OnMatchOver;

            var groupedTurns = turns.Where(turn => turn.Password.EnglishWord != "END" && turn.Password.SpanishWord != "END")
                .GroupBy(turn => turn.TurnId)
                .Select(group => new ValidationTurnViewModel(group, language));
            TurnsToValidate = new ObservableCollection<ValidationTurnViewModel>(groupedTurns);

            SubmitVotesCommand = new RelayCommand(async (_) => await SubmitVotesAsync(), (_) => CanSubmit);
        }

        private void OnValidationTimerTick(int secondsLeft)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ValidationSeconds = secondsLeft;
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
                    log.InfoFormat("Dispatcher invoked - starting cleanup");
                    Cleanup();
                    log.InfoFormat("Cleanup completed - calling GoBack");
                    windowService.GoBack();
                    log.InfoFormat("Successfully navigated back from validation");
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

            try
            {
                log.InfoFormat("Submitting {0} validation votes for game {1}, player {2}", votes.Count, gameCode, playerId);
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
        }
        private void LogCurrentState()
        {
            try
            {
                log.InfoFormat("Current state - CanSubmit: {0}, TurnsCount: {1}", CanSubmit, TurnsToValidate.Count);
                log.InfoFormat("Application.Current null: {0}", Application.Current == null);
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
                log.Info("Starting cleanup - unsubscribing from events");
                gameManagerService.ValidationTimerTick -= OnValidationTimerTick;
                gameManagerService.ValidationComplete -= OnValidationComplete;
                gameManagerService.MatchCancelled -= OnMatchCancelled;
                gameManagerService.MatchOver -= OnMatchOver;

                log.Info("Cleanup completed successfully");
            }
            catch (Exception ex)
            {
                log.Error($"Error during cleanup: {ex.Message}", ex);
            }
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
                Cleanup();
                gameManagerService.Cleanup();

                windowService.GoToLobby();
            });
        }
    }
}
