using Data.DAL.Interfaces;
using Data.Model;
using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Services.Util;
using Services.Wrappers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class FriendsManager : IFriendsManager
    {
        private readonly ConcurrentDictionary<int, IFriendsCallback> connectedClients; 
        private readonly IFriendshipRepository friendshipRepository;
        private readonly IAccountRepository accountRepository;
        private readonly IOperationContextWrapper operationContext;
        private static readonly ILog log = LogManager.GetLogger(typeof(FriendsManager));
        public FriendsManager(IFriendshipRepository friendshipRepository, IAccountRepository accountRepository, 
            IOperationContextWrapper operationContext)
        {
            connectedClients = new ConcurrentDictionary<int, IFriendsCallback>();
            this.friendshipRepository = friendshipRepository;
            this.accountRepository = accountRepository;
            this.operationContext = operationContext;
        }


        public async Task<List<FriendDTO>> GetFriendsAsync(int userAccountId)
        {
            try
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
            } 
            catch (DbException dbEx)
            {
                log.Error($"DbException in GetFriendsAsync (UserAccountId: {userAccountId})", dbEx);
                throw FaultExceptionFactory.Create(ServiceErrorCode.DatabaseError,
                    "DATABASE_ERROR", "Error querying friends list.");
            } 
            catch (Exception ex)
            {
                log.Fatal($"Unexpected error in GetFriendsAsync (UserAccountId: {userAccountId})", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError, 
                    "UNEXPECTED_ERROR", "Unexpected error retrieving friends list.");
            }
        }

        public async Task<bool> DeleteFriendAsync(int currentPlayerId, int friendToDeleteId)
        {
            try
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
            }
            catch (DbUpdateException dbUpEx)
            {
                log.Error($"DbUpdateException in DeleteFriendAsync: User={currentPlayerId}, " +
                    $"Friend={friendToDeleteId}", dbUpEx);
                throw FaultExceptionFactory.Create(ServiceErrorCode.DatabaseError,
                    "DATABASE_ERROR", "Error saving friend removal.");
            }
            catch (DbException dbEx) 
            {
                log.Error($"DbException in DeleteFriendAsync: User={currentPlayerId}, Friend={friendToDeleteId}", dbEx);
                throw FaultExceptionFactory.Create(ServiceErrorCode.DatabaseError,
                    "DATABASE_ERROR", "Database error deleting friend.");
            }
            catch (Exception ex)
            {
                log.Fatal($"Unexpected error in DeleteFriendAsync: User={currentPlayerId}, Friend={friendToDeleteId}", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError,
                    "UNEXPECTED_ERROR", "Unexpected error removing friend.");
            }
        }

        public Task SubscribeToFriendUpdatesAsync(int userAccountId)
        {
            var callbackChannel = operationContext.GetCallbackChannel<IFriendsCallback>();
            connectedClients[userAccountId] = callbackChannel;

            var communicationObject = (ICommunicationObject)callbackChannel;
            communicationObject.Faulted += (sender, e) => {
                connectedClients.TryRemove(userAccountId, out _);
            };
            communicationObject.Closed += (sender, e) => {
                connectedClients.TryRemove(userAccountId, out _);
            };

            log.InfoFormat("Client subscribed to FriendsManager. UserAccountId: {0}", userAccountId);
            return Task.CompletedTask;
        }

        public async Task<FriendRequestResult> SendFriendRequestAsync(string addresseeEmail)
        {
            int requesterUserAccountId = GetUserAccountIdFromCallback();
            if (requesterUserAccountId == 0)
            {
                log.Warn("SendFriendRequestAsync failed: Could not obtain UserAccountId from callback.");
                return FriendRequestResult.Failed;
            }

            try
            {
                log.InfoFormat("Starting SendFriendRequestAsync: RequesterId={0}, AddresseeEmail={1}",
                    requesterUserAccountId, addresseeEmail);
                return await TrySendFriendRequestAsync(requesterUserAccountId, addresseeEmail);
            }
            catch (DbUpdateException dbUpEx)
            {
                log.Error($"DbUpdateException in SendFriendRequestAsync: RequesterId={requesterUserAccountId}," +
                    $" Email={addresseeEmail}", dbUpEx);
                throw FaultExceptionFactory.Create(ServiceErrorCode.DatabaseError, "DATABASE_ERROR",
                    "Error saving friend request.");
            }
            catch (DbException dbEx)
            {
                log.Error($"DbException in SendFriendRequestAsync: RequesterId={requesterUserAccountId}, " +
                    $"Email={addresseeEmail}", dbEx);
                throw FaultExceptionFactory.Create(ServiceErrorCode.DatabaseError, "DATABASE_ERROR", 
                    "Database error sending friend request.");
            }
            catch (Exception ex)
            {
                log.Fatal($"Unexpected error in SendFriendRequestAsync: RequesterId={requesterUserAccountId}," +
                    $" Email={addresseeEmail}", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError, "UNEXPECTED_ERROR",
                    "Unexpected error sending friend request.");
            }
        }

        public async Task<List<FriendDTO>> GetPendingRequestsAsync()
        {
            int userAccountId = GetUserAccountIdFromCallback();
            if (userAccountId == 0)
            {
                log.Warn("GetPendingRequestsAsync failed: Could not obtain UserAccountId from callback.");
                return new List<FriendDTO>();
            }

            try
            {
                log.InfoFormat("Starting GetPendingRequestsAsync for UserAccountId: {0}", userAccountId);
                var requests = await friendshipRepository.GetPendingRequestsAsync(userAccountId);

                var requestDTOs = requests.Select(req => new FriendDTO
                {
                    PlayerId = req.Player1.Id,
                    Nickname = req.Player1.UserAccount.Nickname,
                }).ToList();

                log.InfoFormat("GetPendingRequestsAsync completed. {0} requests found.", requestDTOs.Count);
                return requestDTOs;
            }
            catch (DbException dbEx)
            {
                log.Error($"DbException in GetPendingRequestsAsync (UserAccountId: {userAccountId})", dbEx);
                throw FaultExceptionFactory.Create(ServiceErrorCode.DatabaseError, "DATABASE_ERROR",
                    "Error querying pending requests.");
            }
            catch (Exception ex)
            {
                log.Fatal($"Unexpected error in GetPendingRequestsAsync (UserAccountId: {userAccountId})", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError, "UNEXPECTED_ERROR",
                    "Unexpected error retrieving pending requests.");
            }
        }

        public async Task RespondToFriendRequestAsync(int requesterPlayerId, bool accepted)
        {
            int addresseeUserAccountId = GetUserAccountIdFromCallback();
            if (addresseeUserAccountId == 0)
            {
                log.Warn("RespondToFriendRequestAsync failed: Could not obtain UserAccountId from callback.");
                return;
            }

            try
            {
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
                    log.WarnFormat("RespondToFriendRequestAsync failed (repository returned false). RequesterId={0}, " +
                        "AddresseeId={1}", requesterPlayerId, addresseePlayerId);
                }
            }
            catch (DbUpdateException dbUpEx)
            {
                log.Error($"DbUpdateException in RespondToFriendRequestAsync: AddresseeId={addresseeUserAccountId}," +
                    $" RequesterId={requesterPlayerId}", dbUpEx);
                throw FaultExceptionFactory.Create(ServiceErrorCode.DatabaseError, "DATABASE_ERROR", 
                    "Error saving request response.");
            }
            catch (DbException dbEx)
            {
                log.Error($"DbException in RespondToFriendRequestAsync: AddresseeId={addresseeUserAccountId}," +
                    $" RequesterId={requesterPlayerId}", dbEx);
                throw FaultExceptionFactory.Create(ServiceErrorCode.DatabaseError, "DATABASE_ERROR", 
                    "Database error responding to request.");
            }
            catch (Exception ex)
            {
                log.Fatal($"Unexpected error in RespondToFriendRequestAsync: AddresseeId={addresseeUserAccountId}, " +
                    $"RequesterId={requesterPlayerId}", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError, "UNEXPECTED_ERROR",
                    "Unexpected error responding to request.");
            }
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

        private async Task<FriendRequestResult> ValidateFriendRequestAsync(UserAccount requesterAccount, UserAccount addresseeAccount)
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

        private async Task<bool> CreateAndNotifyFriendRequestAsync(UserAccount requesterAccount, UserAccount addresseeAccount)
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
                log.WarnFormat("NotifyOnRequestAcceptedAsync: Account or Player not found for requester (PlayerId: {0})", requesterPlayerId);
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

            if (currentUserAccount != null && connectedClients.TryGetValue(currentUserAccount.Id, out var currentCallback))
            {
                currentCallback.OnFriendRemoved(friendToDeleteId);
            }
        }

    }
}
