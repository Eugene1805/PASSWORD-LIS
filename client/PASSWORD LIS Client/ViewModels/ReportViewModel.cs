using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.LoginManagerServiceReference;
using PASSWORD_LIS_Client.ReportManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using PASSWORD_LIS_Client.WaitingRoomManagerServiceReference;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class ReportViewModel : BaseViewModel
    {
        private readonly UserDTO reporter; 
        private readonly PlayerDTO reportedPlayer; 
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
        public string TitleMessage 
        { 
            get; 
            private set; 
        }

        public ICommand SubmitReportCommand 
        { 
            get; 
        }

        public ReportViewModel(UserDTO Reporter, PlayerDTO ReportedPlayer, IWindowService WindowService,
            IReportManagerService ReportManagerService) : base(WindowService)
        {
            this.reporter = Reporter;
            this.reportedPlayer = ReportedPlayer;
            this.reportManagerService = ReportManagerService;

            TitleMessage = $"{Properties.Langs.Lang.reportingText} {ReportedPlayer.Nickname}";

            SubmitReportCommand = new RelayCommand(async (_) => await SubmitReportAsync(),
                (_) => !string.IsNullOrWhiteSpace(ReportReason)
            );
        }

        private async Task SubmitReportAsync()
        {
            try
            {
                await ExecuteAsync(async () =>
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
                        windowService.ShowPopUp(Properties.Langs.Lang.reportSummitedText,
                            Properties.Langs.Lang.thanksForReportText, PopUpIcon.Success);
                    }
                    else
                    {
                        windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText, 
                            Properties.Langs.Lang.couldNotSummitReportText, PopUpIcon.Error);
                    }
                });
            }
            finally
            {
                windowService.CloseWindow(this);
            }
        }
    }
}
