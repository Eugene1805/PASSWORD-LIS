using log4net;
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
        void SubscribeToFriendUpdatesAsync(int userAccountId);
        Task<List<FriendDTO>> GetPendingRequestsAsync();
        Task RespondToFriendRequestAsync(int requesterPlayerId, bool accepted);
        Task UnsubscribeFromFriendUpdatesAsync(int userAccountId);
    }

    public class WcfFriendsManagerService : IFriendsManagerService
    {
        public event Action<FriendDTO> FriendRequestReceived;
        public event Action<FriendDTO> FriendAdded;
        public event Action<int> FriendRemoved;

        private readonly DuplexChannelFactory<IFriendsManager> factory;
        private IFriendsManager proxy;
        private static readonly ILog log = LogManager.GetLogger(typeof(WcfFriendsManagerService));


        public WcfFriendsManagerService()
        {
            var context = new InstanceContext(this);
            factory = new DuplexChannelFactory<IFriendsManager>(context, "*");
            proxy = GetProxy();
        }
        private IFriendsManager GetProxy()
        {
            ICommunicationObject channel = proxy as ICommunicationObject;

            if (proxy == null || channel == null ||
                channel.State == CommunicationState.Closed ||
                channel.State == CommunicationState.Faulted)
            {
                try
                {
                    if (channel != null && channel.State == CommunicationState.Faulted)
                    {
                        channel.Abort();
                    }
                }
                catch (Exception ex)
                {
                    log.WarnFormat("Error aborting previus channel: {0}", ex.Message);
                    throw;
                }
                proxy = factory.CreateChannel();
            }
            return proxy;
        }

        public async Task<List<FriendDTO>> GetFriendsAsync(int userAccountId)
        {
            var result = await GetProxy().GetFriendsAsync(userAccountId);
            return result.ToList();
        }
        public Task<bool> DeleteFriendAsync(int currentUserId, int friendToDeleteId)
        {
            return GetProxy().DeleteFriendAsync(currentUserId, friendToDeleteId);
        }

        public Task<FriendRequestResult> SendFriendRequestAsync(string addresseeEmail)
        {
            return GetProxy().SendFriendRequestAsync(addresseeEmail);
        }

        public void SubscribeToFriendUpdatesAsync(int userAccountId)
        {
            GetProxy().SubscribeToFriendUpdatesAsync(userAccountId);
        }

        public async Task<List<FriendDTO>> GetPendingRequestsAsync()
        {
            var result = await GetProxy().GetPendingRequestsAsync();
            return result.ToList();
        }
        public Task RespondToFriendRequestAsync(int requesterPlayerId, bool accepted)
        {
            return GetProxy().RespondToFriendRequestAsync(requesterPlayerId, accepted);
        }

        public Task UnsubscribeFromFriendUpdatesAsync(int userAccountId)
        {
            try
            {
                if (proxy is ICommunicationObject channel && channel.State == CommunicationState.Opened)
                {
                    return proxy.UnsubscribeFromFriendUpdatesAsync(userAccountId);
                }
            }
            catch (Exception)
            {
                (proxy as ICommunicationObject)?.Abort();
            }
            return Task.CompletedTask;
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
