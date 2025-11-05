using Services.Contracts.Enums;
using System.Runtime.Serialization;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class GuessResultDTO
    {
        [DataMember]
        public bool IsCorrect { get; set; }
        [DataMember]
        public MatchTeam Team { get; set; }
        [DataMember]
        public int NewScore { get; set; }
    }
}
