using System.Runtime.Serialization;

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
