using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class RoundStartStateDTO
    {
        [DataMember]
        public int CurrentRound { get; set; }

        [DataMember]
        public List<PlayerDTO> PlayersWithNewRoles { get; set; }
    }
}