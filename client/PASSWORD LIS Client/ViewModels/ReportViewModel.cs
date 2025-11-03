using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.LoginManagerServiceReference;
using PASSWORD_LIS_Client.ReportManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using PASSWORD_LIS_Client.WaitingRoomManagerServiceReference;
using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class ReportViewModel : BaseViewModel
    {
        private readonly UserDTO reporter; 
        private readonly PlayerDTO reportedPlayer; 
        private readonly IWindowService windowService;
        private readonly IReportManagerService reportManagerService;

        private string reportReason;
        public string ReportReason
        {
            get => reportReason;
            set
            {
                SetProperty(ref reportReason, value);
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
        public string TitleMessage { get; private set; }

        public ICommand SubmitReportCommand { get; }

        public ReportViewModel(UserDTO reporter, PlayerDTO reportedPlayer, IWindowService windowService, IReportManagerService reportManagerService)
        {
            this.reporter = reporter;
            this.reportedPlayer = reportedPlayer;
            this.windowService = windowService;
            this.reportManagerService = reportManagerService;

            TitleMessage = $"{Properties.Langs.Lang.reportingText} {reportedPlayer.Nickname}";

            SubmitReportCommand = new RelayCommand(async (_) => await SubmitReportAsync(),
                (_) => !string.IsNullOrWhiteSpace(ReportReason)
            );
        }

        private async Task SubmitReportAsync()
        {
            try
            {
                bool success = await reportManagerService.SubmitReportAsync(
                    new ReportDTO
                    {
                        ReporterPlayerId = reporter.PlayerId,
                        ReportedPlayerId = reportedPlayer.Id, 
                        Reason = ReportReason
                    }
                    );

                if (success)
                {
                    windowService.ShowPopUp(Properties.Langs.Lang.reportSummitedText, Properties.Langs.Lang.thanksForReportText, PopUpIcon.Success);
                }
                else
                {
                    windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText, Properties.Langs.Lang.couldNotSummitReportText, PopUpIcon.Error);
                }
            }
            catch (TimeoutException)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.timeLimitTitleText,
                    Properties.Langs.Lang.serverTimeoutText, PopUpIcon.Warning);
            }
            catch (EndpointNotFoundException)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.connectionErrorTitleText,
                    Properties.Langs.Lang.serverConnectionInternetErrorText, PopUpIcon.Error);
            }
            catch (CommunicationException)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.networkErrorTitleText,
                    Properties.Langs.Lang.serverCommunicationErrorText, PopUpIcon.Error);
            }
            catch (Exception)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
            }
            finally
            {
                windowService.CloseWindow(this);
            }
        }
    }
}
