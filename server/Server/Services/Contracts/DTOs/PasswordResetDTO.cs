using System.Runtime.Serialization;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class PasswordResetDTO
    {
        [DataMember]
        public string Email { get; set; }

        [DataMember]
        public string ResetCode { get; set; }

        [DataMember]
        public string NewPassword { get; set; }
    }
}
