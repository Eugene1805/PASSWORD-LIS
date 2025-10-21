using System.Runtime.Serialization;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class PlayerDTO
    {
        [DataMember]
        public int Id { get; set; }
        [DataMember]
        public string Nickname { get; set; }

        [DataMember]
        public string Role { get; set; }

        [DataMember]
        public bool IsReady { get; set; }
    }
}
