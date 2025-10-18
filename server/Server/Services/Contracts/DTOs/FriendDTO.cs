using System.Runtime.Serialization;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class FriendDTO
    {
        [DataMember]
        public int PlayerId { get; set; }
        [DataMember]
        public string Nickname { get; set; }
        
        //Pensar si agregar la foto.

    }
}
