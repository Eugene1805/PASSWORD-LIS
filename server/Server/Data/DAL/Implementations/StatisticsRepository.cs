using Data.DAL.Interfaces;
using Data.Model;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace Data.DAL.Implementations
{
    public class StatisticsRepository : IStatisticsRepository
    {
        private readonly IDbContextFactory contextFactory;
        public StatisticsRepository(IDbContextFactory contextFactory)
        {
            this.contextFactory = contextFactory;
        }
        public async Task<List<Team>> GetTopTeamsAsync(int numberOfTeams)
        {
            using (var context = contextFactory.CreateDbContext())
            {
                var topTeamsEntities = await context.Team
                    .Include(t => t.Player.Select(p => p.UserAccount))
                    .Where(t => t.Player.Any())
                    .OrderByDescending(t => t.TotalPoints)
                    .Take(numberOfTeams)
                    .ToListAsync();

                return topTeamsEntities ?? new List<Team>();
            }
        }
    }
}
