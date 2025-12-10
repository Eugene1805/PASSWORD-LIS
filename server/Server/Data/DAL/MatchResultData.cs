using System.Collections.Generic;

namespace Data.DAL
{
    public class MatchResultData
    {
        public int RedScore { get; }
        public int BlueScore { get; }
        public IEnumerable<int> RedTeamPlayerIds { get; }
        public IEnumerable<int> BlueTeamPlayerIds { get; }

        public MatchResultData(int redScore, int blueScore, 
            IEnumerable<int> redTeamPlayerIds, IEnumerable<int> blueTeamPlayerIds)
        {
            RedScore = redScore;
            BlueScore = blueScore;
            RedTeamPlayerIds = redTeamPlayerIds;
            BlueTeamPlayerIds = blueTeamPlayerIds;
        }
    }
}
