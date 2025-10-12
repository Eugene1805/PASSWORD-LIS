using Data.DAL.Interfaces;
using Data.Model;
using Data.Util;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace Data.DAL.Implementations
{
    public class StatisticsRepository : IStatisticsRepository
    {
        public async Task<List<Team>> GetTopTeamsAsync(int numberOfTeams)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                var topTeamsEntities = await context.Team
                    .Include(t => t.Player.Select(p => p.UserAccount))
                    .OrderByDescending(t => t.TotalPoints)
                    .Take(numberOfTeams)
                    .ToListAsync();

                return topTeamsEntities;
            }
        }
    }
}
