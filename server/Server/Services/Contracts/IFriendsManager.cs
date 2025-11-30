using Services.Contracts.DTOs;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Contracts
{
    /// <summary>
    /// Manages friend relationships and associated real-time notifications.
    /// </summary>
    [ServiceContract(CallbackContract = typeof(IFriendsCallback))]
    public interface IFriendsManager
    {
        /// <summary>
        /// Subscribes a user to receive friend updates.
        /// </summary>
        /// <param name="userAccountId">The user account identifier.</param>
        [OperationContract(IsOneWay = true)]
        void SubscribeToFriendUpdatesAsync(int userAccountId);

        /// <summary>
        /// Retrieves the friend's list for a user.
        /// </summary>
        /// <param name="userAccountId">The user account identifier.</param>
        /// <returns>List of current friends.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task<List<FriendDTO>> GetFriendsAsync(int userAccountId);

        /// <summary>
        /// Sends a friend request to a user by email.
        /// </summary>
        /// <param name="addresseeEmail">The target user's email.</param>
        /// <returns>The result of the request attempt.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task<FriendRequestResult> SendFriendRequestAsync(string addresseeEmail);

        /// <summary>
        /// Deletes a friend relationship.
        /// </summary>
        /// <param name="currentPlayerId">The requesting player's id.</param>
        /// <param name="friendToDeleteId">The friend's player id to remove.</param>
        /// <returns>True if removed; otherwise, false.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task<bool> DeleteFriendAsync(int currentPlayerId, int friendToDeleteId);

        /// <summary>
        /// Retrieves pending incoming friend requests.
        /// </summary>
        /// <returns>List of pending friend request DTOs.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task<List<FriendDTO>> GetPendingRequestsAsync();

        /// <summary>
        /// Responds to a specific friend request.
        /// </summary>
        /// <param name="requesterPlayerId">The requester's player id.</param>
        /// <param name="accepted">True to accept; false to decline.</param>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task RespondToFriendRequestAsync(int requesterPlayerId, bool accepted);

        /// <summary>
        /// Unsubscribes the user from friend updates.
        /// </summary>
        /// <param name="userAccountId">The user account identifier.</param>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task UnsubscribeFromFriendUpdatesAsync(int userAccountId);
    }

    /// <summary>
    /// Callback contract for friend-related real-time events.
    /// </summary>
    [ServiceContract]
    public interface IFriendsCallback
    {
        /// <summary>
        /// Notifies that a friend request was received.
        /// </summary>
        /// <param name="requester">The requester friend DTO.</param>
        [OperationContract(IsOneWay = true)]
        void OnFriendRequestReceived(FriendDTO requester);

        /// <summary>
        /// Notifies that a new friend was added.
        /// </summary>
        /// <param name="newFriend">The new friend DTO.</param>
        [OperationContract(IsOneWay = true)]
        void OnFriendAdded(FriendDTO newFriend);

        /// <summary>
        /// Notifies that an existing friend was removed.
        /// </summary>
        /// <param name="friendPlayerId">The removed friend's player id.</param>
        [OperationContract(IsOneWay = true)]
        void OnFriendRemoved(int friendPlayerId);
    }

    /// <summary>
    /// Represents the possible outcomes when sending a friend request.
    /// </summary>
    [DataContract]
    public enum FriendRequestResult
    {
        [EnumMember] Success,
        [EnumMember] UserNotFound,
        [EnumMember] AlreadyFriends,
        [EnumMember] RequestAlreadySent,
        [EnumMember] Failed,
        [EnumMember] RequestAlreadyReceived,
        [EnumMember] CannotAddSelf
    }
}
