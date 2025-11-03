using System.Runtime.Serialization;
using Services.Contracts.Enums;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class ServiceErrorDetailDTO
    {
        [DataMember]
        public ServiceErrorCode Code { get; set; }

        [DataMember]
        public string ErrorCode { get; set; }

        [DataMember]
        public string Message { get; set; }
    }
}
