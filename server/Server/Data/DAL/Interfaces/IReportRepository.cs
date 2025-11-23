using Data.Model;
using System.Threading.Tasks;

namespace Data.DAL.Interfaces
{
    public interface IReportRepository
    {
        Task AddReportAsync(Report report);
        Task<int> GetReportCountForPlayerSinceAsync(int reportedPlayerId, System.DateTime? since);
        Task<bool> HasReporterReportedSinceAsync(int reporterPlayerId, int reportedPlayerId, System.DateTime? since);
    }
}
