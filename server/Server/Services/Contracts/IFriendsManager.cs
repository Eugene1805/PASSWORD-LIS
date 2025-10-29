using Services.Contracts.DTOs;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Contracts
{
    [ServiceContract(CallbackContract = typeof(IFriendsCallback))]
    public interface IFriendsManager
    {
        [OperationContract(IsOneWay = true)]
        Task SubscribeToFriendUpdatesAsync(int userAccountId);

        [OperationContract]
        Task<List<FriendDTO>> GetFriendsAsync(int userAccountId);

        [OperationContract]
        Task<FriendRequestResult> SendFriendRequestAsync(string addresseeEmail);

        [OperationContract]
        Task<bool> DeleteFriendAsync(int currentPlayerId, int friendToDeleteId);

        [OperationContract]
        Task<List<FriendDTO>> GetPendingRequestsAsync();

        [OperationContract]
        Task RespondToFriendRequestAsync(int requesterPlayerId, bool accepted);

        [OperationContract]
        Task UnsubscribeFromFriendUpdatesAsync(int userAccountId);
    }

    public interface IFriendsCallback
    {
        [OperationContract(IsOneWay = true)]
        void OnFriendRequestReceived(FriendDTO requester);

        [OperationContract(IsOneWay = true)]
        void OnFriendAdded(FriendDTO newFriend);

        [OperationContract(IsOneWay = true)]
        void OnFriendRemoved(int friendPlayerId);
    }

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
