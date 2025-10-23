using PASSWORD_LIS_Client.ReportManagerServiceReference;
using System;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.Services
{
    public interface IReportManagerService
    {
        Task<bool> SubmitReportAsync(ReportDTO reportDTO);
    }

    public class WcfReportManagerService : IReportManagerService
    {
        public async Task<bool> SubmitReportAsync(ReportDTO reportDTO)
        {
            var proxy = new ReportManagerClient();
            try
            {
                return await proxy.SubmitReportAsync(reportDTO);
            }
            catch (Exception)
            {
                Console.WriteLine("Error al enviar el reporte");
                throw;
            }
        }
    }
}
