using Data.Model;
using System.Runtime.Serialization;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class TurnHistoryDTO
    {
        [DataMember]
        public int TurnId { get; set; } // Temp ID for the votes
        [DataMember]
        public PasswordWordDTO Password { get; set; }
        [DataMember]
        public string ClueUsed { get; set; }
    }
}
