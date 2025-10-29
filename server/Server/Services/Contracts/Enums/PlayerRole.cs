using System.Runtime.Serialization;

namespace Services.Contracts.Enums
{
    [DataContract]
    public enum PlayerRole
    {
        [EnumMember]
        ClueGuy,
        [EnumMember]
        Guesser
    }
}