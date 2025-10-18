using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class TeamDTO
    {
        [DataMember]
        public int Score { get; set; }
        [DataMember] 
        public List<string> PlayersNicknames { get; set; }

    }
}
