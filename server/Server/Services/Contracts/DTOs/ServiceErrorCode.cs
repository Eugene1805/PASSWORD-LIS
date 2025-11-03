using System.Runtime.Serialization;

namespace Services.Contracts.DTOs
{
 [DataContract]
 public enum ServiceErrorCode
 {
 [EnumMember(Value = "USER_ALREADY_EXISTS")]
 UserAlreadyExists,
 [EnumMember(Value = "DATABASE_ERROR")]
 DatabaseError,
 [EnumMember(Value = "UNEXPECTED_ERROR")]
 UnexpectedError,

 [EnumMember(Value = "STATISTICS_ERROR")]
 StatisticsError,

 [EnumMember(Value = "COULD_NOT_CREATE_ROOM")]
 CouldNotCreateRoom,
 [EnumMember(Value = "ROOM_NOT_FOUND")]
 RoomNotFound,
 [EnumMember(Value = "ROOM_FULL")]
 RoomFull,
 [EnumMember(Value = "PLAYER_NOT_FOUND")]
 PlayerNotFound,
 [EnumMember(Value = "ALREADY_IN_ROOM")]
 AlreadyInRoom
 }
}
