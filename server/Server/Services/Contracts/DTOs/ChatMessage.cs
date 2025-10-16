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
        public string SenderUsername { get; set; }

        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
