using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

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
