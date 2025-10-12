using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class UserDTO
    {
        [DataMember]
        public int PlayerId { get; set; }

        [DataMember]
        public string Nickname { get; set; }

        [DataMember]
        public string Email { get; set; }

        [DataMember]
        public string FirstName { get; set; }
        
        [DataMember]
        public string LastName { get; set; }
        
        [DataMember]
        public int PhotoId { get; set; }

        [DataMember]
        public Dictionary<string, string> SocialAccounts { get; set; }

    }
}
