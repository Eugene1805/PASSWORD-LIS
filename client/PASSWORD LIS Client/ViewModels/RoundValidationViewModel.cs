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
using System.Windows.Input;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class RoundValidationViewModel : BaseViewModel
    {
        public ObservableCollection<ValidationTurnViewModel> TurnsToValidate { get; }
        private int validationSeconds = 20;
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

        public RoundValidationViewModel(TurnHistoryDTO[] turns, IGameManagerService gameManagerService, IWindowService windowService,
            string gameCode, int playerId, string language)
        {
            this.gameManagerService = gameManagerService;
            this.windowService = windowService;
            this.gameCode = gameCode;
            this.playerId = playerId;
            this.language = language;

            gameManagerService.ValidationTimerTick += OnValidationTimerTick;
            gameManagerService.ValidationComplete += OnValidationComplete;

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
            Application.Current.Dispatcher.Invoke(() =>
            {
                Cleanup();
                windowService.GoBack();
            });
        }

        private async Task SubmitVotesAsync()
        {
            CanSubmit = false;
            var votes = new List<ValidationVoteDTO>();
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
                await gameManagerService.SubmitValidationVotesAsync(gameCode, playerId, votes.ToArray());
            }
            catch (Exception ex)
            {
                HandleConnectionError(ex,"Error al enviar los votos" ); //errorSendingVotesText
                CanSubmit = true; // Permitir reintento si falló
            }
        }
        private void Cleanup()
        {
            gameManagerService.ValidationTimerTick -= OnValidationTimerTick;
            gameManagerService.ValidationComplete -= OnValidationComplete;
        }
        // --- MÉTODO DE MANEJO DE EXCEPCIONES  ---
        private void HandleConnectionError(Exception ex, string customMessage)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Usamos '??' como "Plan B" por si faltan los strings de traducción
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

                // Limpiamos y volvemos a la página de juego
                Cleanup();
                windowService.GoBack();
            });
        }
    }
}
