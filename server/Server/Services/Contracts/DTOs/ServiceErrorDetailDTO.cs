using System.Runtime.Serialization;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class ServiceErrorDetailDTO
    {
        [DataMember]
        public string ErrorCode { get; set; }

        [DataMember]
        public string Message { get; set; }
    }
}
