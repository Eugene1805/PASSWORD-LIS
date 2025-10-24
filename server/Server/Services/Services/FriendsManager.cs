using Data.DAL.Implementations;
using Data.DAL.Interfaces;
using Data.Model;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Wrappers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity.Core.Mapping;
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
        public FriendsManager(IFriendshipRepository friendshipRepository, IAccountRepository accountRepository, IOperationContextWrapper operationContext)
        {
            connectedClients = new ConcurrentDictionary<int, IFriendsCallback>();
            this.friendshipRepository = friendshipRepository;
            this.accountRepository = accountRepository;
            this.operationContext = operationContext;
        }


        public Task<List<FriendDTO>> GetFriendsAsync(int userAccountId)
        {
            var friendAccounts = friendshipRepository.GetFriendsByUserAccountId(userAccountId);

            var friendDTOs = friendAccounts.Select(acc => new FriendDTO
            {
                PlayerId = acc.Player.First().Id,
                Nickname = acc.Nickname,
            }).ToList();

            return Task.FromResult(friendDTOs);
        }
        public Task<bool> DeleteFriendAsync(int currentUserId, int friendToDeleteId)
        {
            bool success = friendshipRepository.DeleteFriendship(currentUserId, friendToDeleteId);

            if (success)
            {
                var currentUserAccount = accountRepository.GetUserByPlayerId(currentUserId);
                var friendAccount = accountRepository.GetUserByPlayerId(friendToDeleteId);

                if (connectedClients.TryGetValue(friendAccount.Id, out var friendCallback))
                {
                    friendCallback.OnFriendRemoved(currentUserId);
                }

                if (connectedClients.TryGetValue(currentUserAccount.Id, out var currentCallback))
                {
                    currentCallback.OnFriendRemoved(friendToDeleteId);
                }
            }

            return Task.FromResult(success);
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

            return Task.CompletedTask;
        }

        public Task<FriendRequestResult> SendFriendRequestAsync(string addresseeEmail)
        {
            int requesterUserAccountId = GetUserAccountIdFromCallback();
            if (requesterUserAccountId == 0)
            {
                return Task.FromResult(FriendRequestResult.Failed);

            }
         
            var requesterAccount = accountRepository.GetUserByUserAccountId(requesterUserAccountId);
            var addresseeAccount = accountRepository.GetUserByEmail(addresseeEmail);

            if (addresseeAccount == null)
            {
                return Task.FromResult(FriendRequestResult.UserNotFound);
            }
            
            if (requesterAccount == null)
            { 
                return Task.FromResult(FriendRequestResult.Failed);
            }
            if (requesterAccount.Id == addresseeAccount.Id)
            {
                return Task.FromResult(FriendRequestResult.Failed);
            }

            var validationResult = ValidateFriendRequest(requesterAccount, addresseeAccount);

            if (validationResult != FriendRequestResult.Success)
            {
                return Task.FromResult(validationResult);
            }

            bool success = CreateAndNotifyFriendRequest(requesterAccount, addresseeAccount);
            if (success)
            {
                return Task.FromResult(FriendRequestResult.Success);
            }
            else
            {
                return Task.FromResult(FriendRequestResult.Failed);
            }            
        }
        
        public Task<List<FriendDTO>> GetPendingRequestsAsync()
        {
            int userAccountId = GetUserAccountIdFromCallback();
            if (userAccountId == 0)
            {
                return Task.FromResult(new List<FriendDTO>());
            }
            
            var requests = friendshipRepository.GetPendingRequests(userAccountId);

            var requestDTOs = requests.Select(req => new FriendDTO
            {
                PlayerId = req.Player1.Id, // El ID del que envió la solicitud
                Nickname = req.Player1.UserAccount.Nickname
            }).ToList();

            return Task.FromResult(requestDTOs);
        }

        public Task RespondToFriendRequestAsync(int requesterPlayerId, bool accepted)
        {
            int addreseeUserAccountId = GetUserAccountIdFromCallback();
            if (addreseeUserAccountId == 0)
            {
                return Task.CompletedTask;

            }

            var addresseeAccount = accountRepository.GetUserByUserAccountId(addreseeUserAccountId); 

            if (addresseeAccount == null || !addresseeAccount.Player.Any())
            {
                return Task.CompletedTask;
            }

            int addresseePlayerId = addresseeAccount.Player.First().Id;

            bool success = friendshipRepository.RespondToFriendRequest(requesterPlayerId, addresseePlayerId, accepted);

            if (success && accepted)
            {
                var requesterAccount = accountRepository.GetUserByPlayerId(requesterPlayerId);

                if (requesterAccount == null)
                {
                    return Task.CompletedTask;
                }

                var callback = connectedClients[addreseeUserAccountId];

                var requesterDto = new FriendDTO { 
                    PlayerId = requesterPlayerId,
                    Nickname = requesterAccount.Nickname 
                };

                callback.OnFriendAdded(requesterDto);

                if (connectedClients.TryGetValue(requesterAccount.Id, out var requesterCallback))
                {
                    var addresseeDto = new FriendDTO { 
                        PlayerId = addresseePlayerId, 
                        Nickname = addresseeAccount.Nickname 
                    };
                    requesterCallback.OnFriendAdded(addresseeDto);
                }
            }

            return Task.CompletedTask;
        }

        public Task UnsubscribeFromFriendUpdatesAsync(int userAccountId)
        {
            connectedClients.TryRemove(userAccountId, out _);
            return Task.CompletedTask;
        }

        private int GetUserAccountIdFromCallback()
        {
            var callback = operationContext.GetCallbackChannel<IFriendsCallback>();
            var entry = connectedClients.FirstOrDefault(pair => pair.Value == callback);
            
            return entry.Key;
        }

        private FriendRequestResult ValidateFriendRequest(UserAccount requesterAccount, UserAccount addresseeAccount)
        {
            int requesterPlayerId = requesterAccount.Player.First().Id;
            int addresseePlayerId = addresseeAccount.Player.First().Id;

            // Comprobar si ya son amigos
            var friends = friendshipRepository.GetFriendsByUserAccountId(requesterAccount.Id);
            if (friends.Any(f => f.Player.Any(p => p.Id == addresseePlayerId)))
            {
                return FriendRequestResult.AlreadyFriends;
            }

            // Comprobar si una solicitud (A -> B) ya está pendiente
            var addresseeRequests = friendshipRepository.GetPendingRequests(addresseeAccount.Id);
            if (addresseeRequests.Any(req => req.Player1.Id == requesterPlayerId)) // Player1 es quien envía
            {
                return FriendRequestResult.RequestAlreadySent;
            }

            // Comprobar si una solicitud (B -> A) ya fue recibida
            var requesterRequests = friendshipRepository.GetPendingRequests(requesterAccount.Id);
            if (requesterRequests.Any(req => req.Player1.Id == addresseePlayerId)) // Player1 es quien envía
            {
                return FriendRequestResult.RequestAlreadyReceived;
            }

            return FriendRequestResult.Success;
        }

        private bool CreateAndNotifyFriendRequest(UserAccount requesterAccount, UserAccount addresseeAccount)
        {
            int requesterPlayerId = requesterAccount.Player.First().Id;
            int addresseePlayerId = addresseeAccount.Player.First().Id;

            bool success = friendshipRepository.CreateFriendRequest(requesterPlayerId, addresseePlayerId);

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
    }
}
