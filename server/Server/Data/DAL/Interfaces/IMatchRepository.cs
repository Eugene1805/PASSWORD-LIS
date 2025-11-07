using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
