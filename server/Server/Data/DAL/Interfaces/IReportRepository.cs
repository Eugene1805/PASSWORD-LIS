using Data.Model;
using System.Threading.Tasks;

namespace Data.DAL.Interfaces
{
    public interface IReportRepository
    {
        Task AddReportAsync(Report report);
        Task<int> GetReportCountForPlayerAsync(int reportedPlayerId);
    }
}
