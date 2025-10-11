using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

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
