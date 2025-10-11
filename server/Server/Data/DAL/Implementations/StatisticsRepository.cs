using Data.DAL.Interfaces;
using Data.Model;
using Data.Util;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace Data.DAL.Implementations
{
    public class StatisticsRepository : IStatisticsRepository
    {
        public List<Team> GetTopTeams(int numberOfTeams)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                var topTeamsEntities = context.Team
                    .Include(t => t.Player.Select(p => p.UserAccount))
                    .OrderByDescending(t => t.TotalPoints)
                    .Take(numberOfTeams)
                    .ToList();

                return topTeamsEntities;
            }
        }
    }
}
