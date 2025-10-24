using Data.DAL.Interfaces;
using Data.Model;
using Services.Contracts;
using Services.Contracts.DTOs;
using System;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class ReportManager : IReportManager
    {
        private readonly IReportRepository reportRepository;

        public ReportManager(IReportRepository reportRepository)
        {
            this.reportRepository = reportRepository;
        }

        public async Task<bool> SubmitReportAsync(ReportDTO reportDTO)
        {
            try
            {
                var newReport = new Report
                {
                    ReporterPlayerId = reportDTO.ReporterPlayerId,
                    ReportedPlayerId = reportDTO.ReportedPlayerId,
                    Reason = reportDTO.Reason
                };

                await reportRepository.AddReportAsync(newReport);

                return true; 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al guardar el reporte: {ex.Message}");
                return false;
            }
        }

    }
}
