using Data.DAL.Interfaces;
using Data.Model;
using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Wrappers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class FriendsManager : ServiceBase, IFriendsManager
    {
        private readonly ConcurrentDictionary<int, IFriendsCallback> connectedClients; 
        private readonly IFriendshipRepository friendshipRepository;
        private readonly IAccountRepository accountRepository;
        private readonly IOperationContextWrapper operationContext;
        private static readonly ILog log = LogManager.GetLogger(typeof(FriendsManager));
        private const int InvalidUserId = 0;
        public FriendsManager(IFriendshipRepository friendshipRepository, IAccountRepository accountRepository, 
            IOperationContextWrapper operationContext) : base(log)
        {
            connectedClients = new ConcurrentDictionary<int, IFriendsCallback>();
            this.friendshipRepository = friendshipRepository;
            this.accountRepository = accountRepository;
            this.operationContext = operationContext;
        }
        public async Task<List<FriendDTO>> GetFriendsAsync(int userAccountId)
        {
            return await ExecuteAsync(async () =>
            {
                log.InfoFormat("Starting GetFriendsAsync for UserAccountId: {0}", userAccountId);
                var friendAccounts = await friendshipRepository.GetFriendsByUserAccountIdAsync(userAccountId);

                var friendDTOs = friendAccounts.Select(acc => new FriendDTO
                {
                    PlayerId = acc.Player.First().Id,
                    Nickname = acc.Nickname,
                }).ToList();

                log.InfoFormat("GetFriendsAsync completed. {0} friends found.", friendDTOs.Count);
                return friendDTOs;
            }, context: "FriendManager: GetFriendsAsync");
            
        }

        public async Task<bool> DeleteFriendAsync(int currentPlayerId, int friendToDeleteId)
        {
            return await ExecuteAsync(async () =>
            {
                log.InfoFormat("Starting DeleteFriendAsync: User={0}, Friend={1}", currentPlayerId, friendToDeleteId);
                bool success = await friendshipRepository.DeleteFriendshipAsync(currentPlayerId, friendToDeleteId);

                if (success)
                {
                    log.InfoFormat("Friendship deleted User={0}, Friend={1}", currentPlayerId, friendToDeleteId);
                    await NotifyFriendRemovedAsync(currentPlayerId, friendToDeleteId);
                }
                else
                {
                    log.WarnFormat("DeleteFriendAsync failed: User={0}, Friend={1}", currentPlayerId, friendToDeleteId);
                }
                return success;
            }, context: $"User={currentPlayerId}, Friend={friendToDeleteId}");
        }

        public void SubscribeToFriendUpdatesAsync(int userAccountId)
        {
            var callbackChannel = operationContext.GetCallbackChannel<IFriendsCallback>();
            connectedClients[userAccountId] = callbackChannel;

            var communicationObject = (ICommunicationObject)callbackChannel;
            communicationObject.Faulted += (sender, e) => 
            {
                connectedClients.TryRemove(userAccountId, out _);
            };
            communicationObject.Closed += (sender, e) => 
            {
                connectedClients.TryRemove(userAccountId, out _);
            };

            log.InfoFormat("Client subscribed to FriendsManager. UserAccountId: {0}", userAccountId);
            
        }

        public async Task<FriendRequestResult> SendFriendRequestAsync(string addresseeEmail)
        {
            return await ExecuteAsync(async () =>
            {
                int requesterUserAccountId = GetUserAccountIdFromCallback();
                if (requesterUserAccountId == InvalidUserId)
                {
                    log.Warn("SendFriendRequestAsync failed: Could not obtain UserAccountId from callback.");
                    return FriendRequestResult.Failed;
                }
                log.InfoFormat("Starting SendFriendRequestAsync: RequesterId={0}, AddresseeEmail={1}",
                    requesterUserAccountId, addresseeEmail);
                return await TrySendFriendRequestAsync(requesterUserAccountId, addresseeEmail);
            }, context: "FriendManager: SendFriendRequestAsync");
        }

        public async Task<List<FriendDTO>> GetPendingRequestsAsync()
        {
            return await ExecuteAsync(async () =>
            {
                int userAccountId = GetUserAccountIdFromCallback();
                if (userAccountId == InvalidUserId)
                {
                    log.Warn("GetPendingRequestsAsync failed: Could not obtain UserAccountId from callback.");
                    return new List<FriendDTO>();
                }
                log.InfoFormat("Starting GetPendingRequestsAsync for UserAccountId: {0}", userAccountId);
                var requests = await friendshipRepository.GetPendingRequestsAsync(userAccountId);

                var requestDTOs = requests.Select(req => new FriendDTO
                {
                    PlayerId = req.Player1.Id,
                    Nickname = req.Player1.UserAccount.Nickname,
                }).ToList();

                log.InfoFormat("GetPendingRequestsAsync completed. {0} requests found.", requestDTOs.Count);
                return requestDTOs;

            }, context: "FriendManager: GetPendingRequestsAsync");
        }

        public async Task RespondToFriendRequestAsync(int requesterPlayerId, bool accepted)
        {
            await ExecuteAsync(async () =>
            {
                int addresseeUserAccountId = GetUserAccountIdFromCallback();
                if (addresseeUserAccountId == InvalidUserId)
                {
                    log.Warn("RespondToFriendRequestAsync failed: Could not obtain UserAccountId from callback.");
                    return;
                }
                log.InfoFormat("Starting RespondToFriendRequestAsync: AddresseeId={0}, RequesterId={1}, Accepted={2}",
                    addresseeUserAccountId, requesterPlayerId, accepted);

                var addresseeAccount = await accountRepository.GetUserByUserAccountIdAsync(addresseeUserAccountId);

                if (addresseeAccount == null || !addresseeAccount.Player.Any())
                {
                    log.WarnFormat("RespondToFriendRequestAsync: Account or Player not found for addressee (Id: {0})",
                        addresseeUserAccountId);
                    return;
                }

                int addresseePlayerId = addresseeAccount.Player.First().Id;

                bool success = await friendshipRepository.RespondToFriendRequestAsync(requesterPlayerId,
                    addresseePlayerId, accepted);

                if (success && accepted)
                {
                    log.InfoFormat("Friend request accepted. Notifying... RequesterId={0}, AddresseeId={1}",
                        requesterPlayerId, addresseePlayerId);
                    await NotifyOnRequestAcceptedAsync(requesterPlayerId, addresseeAccount);
                }
                else if (!success)
                {
                    log.WarnFormat("RespondToFriendRequestAsync failed (repository returned false). RequesterId={0}," +
                        "AddresseeId={1}", requesterPlayerId, addresseePlayerId);
                }

            }, context: "FriendManager: RespondToFriendRequestAsync");
        }

        public Task UnsubscribeFromFriendUpdatesAsync(int userAccountId)
        {
            connectedClients.TryRemove(userAccountId, out _);
            log.InfoFormat("Client unsubscribed from FriendsManager. UserAccountId: {0}", userAccountId);
            return Task.CompletedTask;
        }

        private int GetUserAccountIdFromCallback()
        {
            var callback = operationContext.GetCallbackChannel<IFriendsCallback>();
            var entry = connectedClients.FirstOrDefault(pair => pair.Value == callback);
            return entry.Key;
        }

        private async Task<FriendRequestResult> TrySendFriendRequestAsync(int requesterUserAccountId,
            string addresseeEmail)
        {
            var requesterAccount = await accountRepository.GetUserByUserAccountIdAsync(requesterUserAccountId);
            var addresseeAccount = await accountRepository.GetUserByEmailAsync(addresseeEmail);

            if (addresseeAccount == null)
            {
                return FriendRequestResult.UserNotFound;
            }
            if (requesterAccount == null || !requesterAccount.Player.Any())
            {
                log.WarnFormat("TrySendFriendRequestAsync: Account or Player not found for requester (Id: {0})",
                    requesterUserAccountId);
                return FriendRequestResult.Failed;
            }
            if (requesterAccount.Id == addresseeAccount.Id)
            {
                return FriendRequestResult.CannotAddSelf;
            }
            if (!addresseeAccount.Player.Any())
            {
                log.WarnFormat("TrySendFriendRequestAsync: Addressee has no associated Player (Email: {0})",
                    addresseeEmail);
                return FriendRequestResult.UserNotFound;
            }

            var validationResult = await ValidateFriendRequestAsync(requesterAccount, addresseeAccount);

            if (validationResult != FriendRequestResult.Success)
            {
                return validationResult;
            }

            bool success = await CreateAndNotifyFriendRequestAsync(requesterAccount, addresseeAccount);
            return success ? FriendRequestResult.Success : FriendRequestResult.Failed;
        }

        private async Task<FriendRequestResult> ValidateFriendRequestAsync(UserAccount requesterAccount, 
            UserAccount addresseeAccount)
        {
            int requesterPlayerId = requesterAccount.Player.First().Id;
            int addresseePlayerId = addresseeAccount.Player.First().Id;

            var friends = await friendshipRepository.GetFriendsByUserAccountIdAsync(requesterAccount.Id);
            if (friends.Any(f => f.Player.Any(p => p.Id == addresseePlayerId)))
            {
                return FriendRequestResult.AlreadyFriends;
            }

            var addresseeRequests = await friendshipRepository.GetPendingRequestsAsync(addresseeAccount.Id);
            if (addresseeRequests.Any(req => req.RequesterId == requesterPlayerId))
            {
                return FriendRequestResult.RequestAlreadySent;
            }

            var requesterRequests = await friendshipRepository.GetPendingRequestsAsync(requesterAccount.Id);
            if (requesterRequests.Any(req => req.RequesterId == addresseePlayerId))
            {
                return FriendRequestResult.RequestAlreadyReceived;
            }

            return FriendRequestResult.Success;
        }

        private async Task<bool> CreateAndNotifyFriendRequestAsync(UserAccount requesterAccount,
            UserAccount addresseeAccount)
        {
            int requesterPlayerId = requesterAccount.Player.First().Id;
            int addresseePlayerId = addresseeAccount.Player.First().Id;

            bool success = await friendshipRepository.CreateFriendRequestAsync(requesterPlayerId, addresseePlayerId);

            if (!success)
            {
                return false;
            }

            if (connectedClients.TryGetValue(addresseeAccount.Id, out var addresseeCallback))
            {
                var requesterDto = new FriendDTO
                {
                    PlayerId = requesterPlayerId,
                    Nickname = requesterAccount.Nickname,
                };
                addresseeCallback.OnFriendRequestReceived(requesterDto);
            }

            return true;
        }

        private async Task NotifyOnRequestAcceptedAsync(int requesterPlayerId, UserAccount addresseeAccount)
        {
            var requesterAccount = await accountRepository.GetUserByPlayerIdAsync(requesterPlayerId);
            if (requesterAccount == null || !requesterAccount.Player.Any())
            {
                log.WarnFormat("NotifyOnRequestAcceptedAsync: " +
                    "Account or Player not found for requester (PlayerId: {0})", requesterPlayerId);
                return;
            }

            int addresseePlayerId = addresseeAccount.Player.First().Id;

            if (connectedClients.TryGetValue(addresseeAccount.Id, out var addresseeCallback))
            {
                var requesterDto = new FriendDTO
                {
                    PlayerId = requesterPlayerId,
                    Nickname = requesterAccount.Nickname,
                };
                addresseeCallback.OnFriendAdded(requesterDto);
            }

            if (connectedClients.TryGetValue(requesterAccount.Id, out var requesterCallback))
            {
                var addresseeDto = new FriendDTO
                {
                    PlayerId = addresseePlayerId,
                    Nickname = addresseeAccount.Nickname,
                };
                requesterCallback.OnFriendAdded(addresseeDto);
            }
        }

        private async Task NotifyFriendRemovedAsync(int currentUserId, int friendToDeleteId)
        {
            var currentUserAccount = await accountRepository.GetUserByPlayerIdAsync(currentUserId);
            var friendAccount = await accountRepository.GetUserByPlayerIdAsync(friendToDeleteId);

            if (friendAccount != null && connectedClients.TryGetValue(friendAccount.Id, out var friendCallback))
            {
                friendCallback.OnFriendRemoved(currentUserId);
            }

            if (currentUserAccount != null && 
                connectedClients.TryGetValue(currentUserAccount.Id, out var currentCallback))
            {
                currentCallback.OnFriendRemoved(friendToDeleteId);
            }
        }
    }
}
