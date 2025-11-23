using Data.DAL.Interfaces;
using Data.Model;
using System;
using System.Data.Entity;
using System.Linq;
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
            if (report == null) throw new ArgumentNullException(nameof(report));
            using (var context = contextFactory.CreateDbContext())
            {
                if (report.CreatedAt == default(DateTime))
                {
                    report.CreatedAt = DateTime.UtcNow;
                }
                context.Report.Add(report);
                await context.SaveChangesAsync();
            }
        }

        public async Task<int> GetReportCountForPlayerSinceAsync(int reportedPlayerId, DateTime? since)
        {
            using (var context = contextFactory.CreateDbContext())
            {
                var query = context.Report.AsQueryable().Where(r => r.ReportedPlayerId == reportedPlayerId);
                if (since.HasValue)
                {
                    query = query.Where(r => r.CreatedAt >= since.Value);
                }
                return await query.CountAsync();
            }
        }

        public async Task<bool> HasReporterReportedSinceAsync(int reporterPlayerId, int reportedPlayerId, DateTime? since)
        {
            using (var context = contextFactory.CreateDbContext())
            {
                var query = context.Report.AsQueryable()
                    .Where(r => r.ReporterPlayerId == reporterPlayerId && r.ReportedPlayerId == reportedPlayerId);
                if (since.HasValue)
                {
                    query = query.Where(r => r.CreatedAt >= since.Value);
                }
                return await query.AnyAsync();
            }
        }
    }
}
