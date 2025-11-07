using Services.Contracts.Enums;
using System.Runtime.Serialization;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class MatchSummaryDTO
    {
        [DataMember]
        public MatchTeam? WinnerTeam { get; set; }
        [DataMember]
        public int RedScore { get; set; }
        [DataMember]
        public int BlueScore { get; set; }
    }
}
