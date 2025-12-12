using Services.Contracts.Enums;
using System.Runtime.Serialization;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class ValidationResultDTO
    {
        [DataMember]
        public MatchTeam TeamThatWasValidated 
        { 
            get;
            set;
        }

        [DataMember]
        public int TotalPenaltyApplied 
        { 
            get;
            set;
        }

        [DataMember]
        public int NewRedTeamScore 
        { 
            get;
            set;
        }

        [DataMember]
        public int NewBlueTeamScore 
        { 
            get;
            set;
        }

    }
}
