using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class MatchInitStateDTO
    {
        [DataMember]
        public List<PlayerDTO> Players { get; set; }
    }
}
