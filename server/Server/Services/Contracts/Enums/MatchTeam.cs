using System.Runtime.Serialization;

namespace Services.Contracts.Enums
{
    [DataContract]
    public enum MatchTeam
    {
        [EnumMember]
        BlueTeam,
        [EnumMember]
        RedTeam
    }
}