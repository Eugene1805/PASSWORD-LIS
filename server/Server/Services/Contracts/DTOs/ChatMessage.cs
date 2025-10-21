using System;
using System.Runtime.Serialization;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class ChatMessage
    {
        [DataMember]
        public int SenderId { get; set; }
        [DataMember]
        public string SenderNickname { get; set; }

        [DataMember]
        public string Message { get; set; }

    }
}
