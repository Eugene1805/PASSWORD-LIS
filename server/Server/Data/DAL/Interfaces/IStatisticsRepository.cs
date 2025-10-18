using Data.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Data.DAL.Interfaces
{
    public interface IStatisticsRepository
    {
        Task<List<Team>> GetTopTeamsAsync(int numberOfTeams);
    }
}
