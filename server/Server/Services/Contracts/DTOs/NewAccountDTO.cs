using System.Runtime.Serialization;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class NewAccountDTO
    {
        [DataMember]
        public string Email 
        { 
            get; 
            set; 
        }

        [DataMember]
        public string Password 
        { 
            get; 
            set; 
        }

        [DataMember]
        public string Nickname 
        { 
            get; 
            set; 
        }
        [DataMember]
        public string FirstName 
        { 
            get; 
            set; 
        }
        [DataMember]
        public string LastName 
        { 
            get; 
            set; 
        }
    }
}
