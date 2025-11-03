using PASSWORD_LIS_Client.FriendsManagerServiceReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.Services
{
    public interface IFriendsManagerService : IFriendsManagerCallback
    {
        event Action<FriendDTO> FriendRequestReceived;
        event Action<FriendDTO> FriendAdded;
        event Action<int> FriendRemoved;

        Task<List<FriendDTO>> GetFriendsAsync(int userAccountId);
        Task<bool> DeleteFriendAsync(int currentUserId, int friendToDeleteId);
        Task<FriendRequestResult> SendFriendRequestAsync(string addresseeEmail);
        Task SubscribeToFriendUpdatesAsync(int userAccountId);
        Task<List<FriendDTO>> GetPendingRequestsAsync();
        Task RespondToFriendRequestAsync(int requesterPlayerId, bool accepted);
        Task UnsubscribeFromFriendUpdatesAsync(int userAccountId);
    }

    public class WcfFriendsManagerService : IFriendsManagerService
    {
        public event Action<FriendDTO> FriendRequestReceived;
        public event Action<FriendDTO> FriendAdded;
        public event Action<int> FriendRemoved;

        private readonly IFriendsManager proxy;

        public WcfFriendsManagerService()
        {
            var context = new InstanceContext(this);
            var factory = new DuplexChannelFactory<IFriendsManager>(context, "*"); //"NetTcpBinding_IFriendsManager"
            proxy = factory.CreateChannel();
        }


        public async Task<List<FriendDTO>> GetFriendsAsync(int userAccountId)
        {
            var result = await proxy.GetFriendsAsync(userAccountId);
            return result.ToList();
        }
        public Task<bool> DeleteFriendAsync(int currentUserId, int friendToDeleteId)
        {
            return proxy.DeleteFriendAsync(currentUserId, friendToDeleteId);
        }

        public Task<FriendRequestResult> SendFriendRequestAsync(string addresseeEmail)
        {
            return proxy.SendFriendRequestAsync(addresseeEmail);
        }

        public Task SubscribeToFriendUpdatesAsync(int userAccountId)
        {
            return proxy.SubscribeToFriendUpdatesAsync(userAccountId);
        }

        public async Task<List<FriendDTO>> GetPendingRequestsAsync()
        {
            var result = await proxy.GetPendingRequestsAsync();
            return result.ToList();
        }
        public Task RespondToFriendRequestAsync(int requesterPlayerId, bool accepted)
        {
            return proxy.RespondToFriendRequestAsync(requesterPlayerId, accepted);
        }

        public Task UnsubscribeFromFriendUpdatesAsync(int userAccountId)
        {
            return proxy.UnsubscribeFromFriendUpdatesAsync(userAccountId);
        }

        public void OnFriendRequestReceived(FriendDTO requester)
        {
            FriendRequestReceived?.Invoke(requester);
        }

        public void OnFriendAdded(FriendDTO newFriend)
        {
            FriendAdded?.Invoke(newFriend);
        }

        public void OnFriendRemoved(int friendPlayerId)
        {
            FriendRemoved?.Invoke(friendPlayerId);
        }

    }
}
