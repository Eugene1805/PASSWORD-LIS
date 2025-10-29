using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.LoginManagerServiceReference;
using PASSWORD_LIS_Client.ReportManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using PASSWORD_LIS_Client.WaitingRoomManagerServiceReference;
using System;
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
                ((RelayCommand)SubmitReportCommand).RaiseCanExecuteChanged();
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

            TitleMessage = $"Reportando a: {reportedPlayer.Nickname}";

            SubmitReportCommand = new RelayCommand(
                execute: async (_) => await SubmitReportAsync(),
                canExecute: (_) => !string.IsNullOrWhiteSpace(ReportReason)
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
                    windowService.ShowPopUp("Reporte Enviado", "Gracias por tu reporte.", PopUpIcon.Success);
                }
                else
                {
                    windowService.ShowPopUp("Error", "No se pudo enviar el reporte.", PopUpIcon.Error);
                }
            }
            catch (Exception)
            {
                windowService.ShowPopUp("Error de Conexión", "Ocurrió un error al contactar al servidor.", PopUpIcon.Error);
            }
            finally
            {
                // Cierra la ventana de reporte
                windowService.CloseWindow(this);
            }
        }
    }
}
