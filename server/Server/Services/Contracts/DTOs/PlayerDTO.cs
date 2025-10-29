using Services.Contracts.Enums;
using System.Runtime.Serialization;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class PlayerDTO
    {
        [DataMember]
        public int Id { get; set; }
        [DataMember]
        public string Nickname { get; set; }
        [DataMember]
        public int PhotoId { get; set; }

        [DataMember]
        public PlayerRole Role { get; set; }
        [DataMember]
        public MatchTeam Team { get; set; }
    }
}
