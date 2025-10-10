using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class EmailVerificationDTO
    {
        [DataMember]
        public string Email { get; set; }
        
        [DataMember]
        public string VerificationCode { get; set; }
    }
}
