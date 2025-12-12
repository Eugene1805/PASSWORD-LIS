using System.Collections.Generic;

namespace Data.DAL
{
    public class MatchResultData
    {
        public int RedScore 
        { 
            get; 
        }
        public int BlueScore 
        { 
            get; 
        }
        public IEnumerable<int> RedTeamPlayerIds 
        { 
            get; 
        }
        public IEnumerable<int> BlueTeamPlayerIds 
        { 
            get; 
        }

        public MatchResultData(int RedScore, int BlueScore, 
            IEnumerable<int> RedTeamPlayerIds, IEnumerable<int> BlueTeamPlayerIds)
        {
            this.RedScore = RedScore;
            this.BlueScore = BlueScore;
            this.RedTeamPlayerIds = RedTeamPlayerIds;
            this.BlueTeamPlayerIds = BlueTeamPlayerIds;
        }
    }
}
