using System.Collections.Generic;
using System.Threading.Tasks;

namespace Data.DAL.Interfaces
{
    public interface IMatchRepository
    {
        Task SaveMatchResultAsync(
            int redScore,
            int blueScore,
            IEnumerable<int> redTeamPlayerIds,
            IEnumerable<int> blueTeamPlayerIds);
    }
}
