using Services.Contracts.DTOs;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Contracts
{
    /// <summary>
    /// Manages waiting room lifecycle and real-time room events.
    /// </summary>
    [ServiceContract(CallbackContract = typeof(IWaitingRoomCallback))]
    public interface IWaitingRoomManager
    {
        /// <summary>
        /// Creates a new waiting room and returns its game code.
        /// </summary>
        /// <param name="email">The host email.</param>
        /// <returns>The generated game code.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task<string> CreateRoomAsync(string email);

        /// <summary>
        /// Joins a room as a registered player.
        /// </summary>
        /// <param name="gameCode">The room code.</param>
        /// <param name="email">The player email.</param>
        /// <returns>The joined player's id.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task<int> JoinRoomAsRegisteredPlayerAsync(string gameCode, string email);

        /// <summary>
        /// Joins a room as a guest using a nickname.
        /// </summary>
        /// <param name="gameCode">The room code.</param>
        /// <param name="nickname">The guest nickname.</param>
        /// <returns>True if joined successfully; otherwise, false.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task<bool> JoinRoomAsGuestAsync(string gameCode, string nickname);

        /// <summary>
        /// Sends a chat message to all room participants.
        /// </summary>
        /// <param name="gameCode">The room code.</param>
        /// <param name="message">The message payload.</param>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task SendMessageAsync(string gameCode, ChatMessageDTO message);

        /// <summary>
        /// Leaves the room.
        /// </summary>
        /// <param name="gameCode">The room code.</param>
        /// <param name="playerId">The leaving player id.</param>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task LeaveRoomAsync(string gameCode, int playerId);

        /// <summary>
        /// Starts the game from the waiting room.
        /// </summary>
        /// <param name="gameCode">The room code.</param>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task StartGameAsync(string gameCode);

        /// <summary>
        /// Retrieves the current players in a room.
        /// </summary>
        /// <param name="gameCode">The room code.</param>
        /// <returns>The list of players.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task<List<PlayerDTO>> GetPlayersInRoomAsync(string gameCode);

        /// <summary>
        /// Notifies that the host has left the room.
        /// </summary>
        /// <param name="gameCode">The room code.</param>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task HostLeftAsync(string gameCode);

        /// <summary>
        /// Sends a game invitation by email.
        /// </summary>
        /// <param name="email">The target email.</param>
        /// <param name="gameCode">The room code.</param>
        /// <param name="inviterNickname">The inviter's nickname.</param>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task SendGameInvitationByEmailAsync(string email, string gameCode, string inviterNickname);

        /// <summary>
        /// Sends a game invitation to a friend account.
        /// </summary>
        /// <param name="friendPlayerId">The friend's player id.</param>
        /// <param name="gameCode">The room code.</param>
        /// <param name="inviterNickname">The inviter's nickname.</param>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task SendGameInvitationToFriendAsync(int friendPlayerId, string gameCode, string inviterNickname);
    }

    /// <summary>
    /// Callback contract for waiting room events.
    /// </summary>
    [ServiceContract]
    public interface IWaitingRoomCallback
    {
        /// <summary>
        /// Notifies that a player joined the room.
        /// </summary>
        /// <param name="player">The player that joined.</param>
        [OperationContract(IsOneWay = true)]
        void OnPlayerJoined(PlayerDTO player);

        /// <summary>
        /// Notifies that a player left the room.
        /// </summary>
        /// <param name="playerId">The id of the player that left.</param>
        [OperationContract(IsOneWay = true)]
        void OnPlayerLeft(int playerId);

        /// <summary>
        /// Delivers a chat message to the client.
        /// </summary>
        /// <param name="message">The message payload.</param>
        [OperationContract(IsOneWay = true)]
        void OnMessageReceived(ChatMessageDTO message);

        /// <summary>
        /// Notifies that the game has started.
        /// </summary>
        [OperationContract(IsOneWay = true)]
        void OnGameStarted();

        /// <summary>
        /// Notifies that the host has left the room.
        /// </summary>
        [OperationContract(IsOneWay = true)]
        void OnHostLeft();
    }
}
