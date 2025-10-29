using System.Runtime.Serialization;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class ChatMessageDTO
    {
        [DataMember]
        public string SenderNickname { get; set; }

        [DataMember]
        public string Message { get; set; }

    }
}
