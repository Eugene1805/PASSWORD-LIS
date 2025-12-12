using System.Runtime.Serialization;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class PasswordWordDTO
    {
        [DataMember]
        public string EnglishWord 
        { 
            get; 
            set;
        }

        [DataMember]
        public string SpanishWord 
        { 
            get; 
            set;
        }

        [DataMember]
        public string SpanishDescription 
        { 
            get;
            set;
        }

        [DataMember]
        public string EnglishDescription 
        { 
            get;
            set;
        }
    }
}
