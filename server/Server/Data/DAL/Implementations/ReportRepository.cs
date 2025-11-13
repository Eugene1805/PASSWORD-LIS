using Data.DAL.Interfaces;
using Data.Model;
using Data.Util;
using System.Data.Entity;
using System.Threading.Tasks;

namespace Data.DAL.Implementations
{
    public class ReportRepository : IReportRepository
    {
        private readonly IDbContextFactory contextFactory;
        public ReportRepository(IDbContextFactory contextFactory)
        {
            this.contextFactory = contextFactory;
        }

        public async Task AddReportAsync(Report report)
        {
            using (var context = contextFactory.CreateDbContext())
            {
                context.Report.Add(report);
                await context.SaveChangesAsync();
            }
        }

        public async Task<int> GetReportCountForPlayerAsync(int reportedPlayerId)
        {
            using (var context = contextFactory.CreateDbContext())
            {
                return await context.Report.CountAsync(r => r.ReportedPlayerId == reportedPlayerId);
            }
        }
    }
}
