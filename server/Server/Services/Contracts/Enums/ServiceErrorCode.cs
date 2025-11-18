using System.Runtime.Serialization;

namespace Services.Contracts.Enums
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
         AlreadyInRoom,
         [EnumMember(Value = "INVALID_REPORT_PAYLOAD")]
         InvalidReportPayload,
         [EnumMember(Value = "REPORTER_NOT_FOUND")]
         ReporterNotFound,
         [EnumMember(Value = "REPORTED_PLAYER_NOT_FOUND")]
         ReportedPlayerNotFound,
         [EnumMember(Value = "PLAYER_ALREADY_BANNED")]
         PlayerAlreadyBanned,
         [EnumMember(Value = "BAN_PERSISTENCE_ERROR")]
         BanPersistenceError,
         [EnumMember(Value = "SUBSCRIPTION_ERROR")]
         SubscriptionError,
         [EnumMember(Value = "UNSUBSCRIPTION_ERROR")]
         UnsubscriptionError,
        [EnumMember(Value = "NULL_ARGUMENT")]
         NullArgument,
        [EnumMember(Value = "EMAIL_SENDING_ERROR")]
         EmailSendingError,
        [EnumMember(Value = "EMAIL_CONFIGURATION_ERROR")]
         EmailConfigurationError
    }
}
