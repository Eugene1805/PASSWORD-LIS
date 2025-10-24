using Data.DAL.Interfaces;
using Data.Model;
using Data.Util;
using System.Threading.Tasks;

namespace Data.DAL.Implementations
{
    public class ReportRepository : IReportRepository
    {
        public async Task AddReportAsync(Report report)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                context.Report.Add(report);
                await context.SaveChangesAsync();
            }
        }
    }
}
