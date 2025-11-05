using System.Runtime.Serialization;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class ValidationVoteDTO
    {
        [DataMember]
        public int TurnId { get; set; }
        [DataMember]
        public bool PenalizeSynonym { get; set; }
        [DataMember]
        public bool PenalizeMultiword { get; set; }
    }
}
