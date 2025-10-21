using Data.DAL.Implementations;
using Data.DAL.Interfaces;
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
        private readonly ConcurrentDictionary<int, IFriendsCallback> connectedClients = new ConcurrentDictionary<int, IFriendsCallback>();
        private readonly IFriendshipRepository friendshipRepository;
        private readonly IAccountRepository accountRepository;
        private readonly IOperationContextWrapper operationContext;
        public FriendsManager(IFriendshipRepository friendshipRepository, IAccountRepository accountRepository, IOperationContextWrapper operationContext)
        {
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
                // Notificar a ambos jugadores por callback
                var currentUserAccount = accountRepository.GetUserByPlayerId(currentUserId);
                var friendAccount = accountRepository.GetUserByPlayerId(friendToDeleteId);

                // Notificar al amigo eliminado (si está conectado)
                if (connectedClients.TryGetValue(friendAccount.Id, out var friendCallback))
                {
                    friendCallback.OnFriendRemoved(currentUserId);
                }

                // Notificar al jugador actual (para que su UI se actualice si otro cliente lo hizo)
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
            connectedClients.TryAdd(userAccountId, callbackChannel);

            return Task.CompletedTask;
        }

        public Task<FriendRequestResult> SendFriendRequestAsync(string addresseeEmail)
        {
            var requesterCallback = operationContext.GetCallbackChannel<IFriendsCallback>();
            var requesterUserAccountId = connectedClients.FirstOrDefault(x => x.Value == requesterCallback).Key;

            var requesterAccount = accountRepository.GetUserByPlayerId(requesterUserAccountId);
            var addresseeAccount = accountRepository.GetUserByEmail(addresseeEmail);

            if (addresseeAccount == null)
            {
                return Task.FromResult(FriendRequestResult.UserNotFound);
            }

            if (requesterAccount == null)
            {
                return Task.FromResult(FriendRequestResult.Failed); // Cuenta del solicitante no encontrada
            }

            if (requesterAccount.Id == addresseeAccount.Id)
            {
                return Task.FromResult(FriendRequestResult.Failed); // No se puede agregar a uno mismo
            }

            int requesterPlayerId = requesterAccount.Player.First().Id;
            int addresseePlayerId = addresseeAccount.Player.First().Id;

            bool success = friendshipRepository.CreateFriendRequest(requesterPlayerId, addresseePlayerId);
            if (!success)
            {
                return Task.FromResult(FriendRequestResult.RequestAlreadySent); // O ya son amigos
            }

            // Notificar al destinatario si está conectado
            if (connectedClients.TryGetValue(addresseeAccount.Id, out var addresseeCallback))
            {
                var requesterDto = new FriendDTO
                {
                    PlayerId = requesterPlayerId,
                    Nickname = requesterAccount.Nickname
                };
                addresseeCallback.OnFriendRequestReceived(requesterDto);
            }
            return Task.FromResult(FriendRequestResult.Success);
        }

        public Task<List<FriendDTO>> GetPendingRequestsAsync()
        {
            var callback = operationContext.GetCallbackChannel<IFriendsCallback>();
            var userAccountId = connectedClients.FirstOrDefault(x => x.Value == callback).Key;

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
            var callback = operationContext.GetCallbackChannel<IFriendsCallback>();
            var addresseeUserAccountId = connectedClients.FirstOrDefault(x => x.Value == callback).Key;
            var addresseeAccount = accountRepository.GetUserByUserAccountId(addresseeUserAccountId); // Necesitarás este método en el repo

            if (addresseeAccount == null || !addresseeAccount.Player.Any())
            {
                // No se encontró la cuenta o no tiene un jugador asociado, no podemos continuar
                return Task.CompletedTask;
            }

            int addresseePlayerId = addresseeAccount.Player.First().Id;

            bool success = friendshipRepository.RespondToFriendRequest(requesterPlayerId, addresseePlayerId, accepted);

            if (success && accepted)
            {
                // Notificar a ambos jugadores que ahora son amigos

                // 1. Notificar al que aceptó (Addressee)
                var requesterAccount = accountRepository.GetUserByPlayerId(requesterPlayerId);
               
                if (requesterAccount == null) return Task.CompletedTask; // Comprobación de seguridad

                var requesterDto = new FriendDTO { 
                    PlayerId = requesterPlayerId,
                    Nickname = requesterAccount.Nickname 
                };
                callback.OnFriendAdded(requesterDto);

                // 2. Notificar al que envió la solicitud (Requester), si está conectado
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
    }
}
