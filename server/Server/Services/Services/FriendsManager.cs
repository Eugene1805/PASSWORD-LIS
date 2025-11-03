using Data.DAL.Implementations;
using Data.DAL.Interfaces;
using Data.Model;
using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Wrappers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity.Core.Mapping;
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
        public FriendsManager(IFriendshipRepository friendshipRepository, IAccountRepository accountRepository, IOperationContextWrapper operationContext)
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
                log.InfoFormat("Iniciando GetFriendsAsync para UserAccountId: {0}", userAccountId);
                var friendAccounts = await Task.Run(() => friendshipRepository.GetFriendsByUserAccountId(userAccountId));

                var friendDTOs = friendAccounts.Select(acc => new FriendDTO
                {
                    PlayerId = acc.Player.First().Id,
                    Nickname = acc.Nickname,
                }).ToList();

                log.InfoFormat("GetFriendsAsync completado. {0} amigos encontrados.", friendDTOs.Count);
                return friendDTOs;
            } 
            catch (DbException dbEx)
            {
                log.Error($"DbException en GetFriendsAsync (UserAccountId: {userAccountId})", dbEx);
                var errorDetail = new ServiceErrorDetailDTO { ErrorCode = "DATABASE_ERROR", Message = "Error al consultar la lista de amigos." };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message));
            } 
            catch (Exception ex)
            {
                log.Fatal($"Error inesperado en GetFriendsAsync (UserAccountId: {userAccountId})", ex);
                var errorDetail = new ServiceErrorDetailDTO { ErrorCode = "UNEXPECTED_ERROR", Message = "Error inesperado al obtener la lista de amigos." };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message));

            }
            
        }
        /*
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
         */
        public async Task<bool> DeleteFriendAsync(int currentUserId, int friendToDeleteId)
        {
            try
            {
                log.InfoFormat("Iniciando DeleteFriendAsync: User={0}, Friend={1}", currentUserId, friendToDeleteId);
                bool success = await Task.Run(() => friendshipRepository.DeleteFriendship(currentUserId, friendToDeleteId));

                if (success)
                {
                    log.InfoFormat("Amistad eliminada. Notificando... User={0}, Friend={1}", currentUserId, friendToDeleteId);
                    await NotifyFriendRemovedAsync(currentUserId, friendToDeleteId);
                }
                else
                {
                    log.WarnFormat("DeleteFriendAsync falló (repositorio devolvió false): User={0}, Friend={1}", currentUserId, friendToDeleteId);
                }
                return success;
            }
            catch (DbUpdateException dbUpEx)
            {
                log.Error($"DbUpdateException en DeleteFriendAsync: User={currentUserId}, Friend={friendToDeleteId}", dbUpEx);
                var errorDetail = new ServiceErrorDetailDTO { ErrorCode = "DATABASE_ERROR", Message = "Error al guardar la eliminación del amigo." };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message));
            }
            catch (DbException dbEx) 
            {
                log.Error($"DbException en DeleteFriendAsync: User={currentUserId}, Friend={friendToDeleteId}", dbEx);
                var errorDetail = new ServiceErrorDetailDTO { ErrorCode = "DATABASE_ERROR", Message = "Error de base de datos al eliminar al amigo." };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message));
            }
            catch (Exception ex)
            {
                log.Fatal($"Error inesperado en DeleteFriendAsync: User={currentUserId}, Friend={friendToDeleteId}", ex);
                var errorDetail = new ServiceErrorDetailDTO { ErrorCode = "UNEXPECTED_ERROR", Message = "Error inesperado al eliminar al amigo." };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message));
            }
        }

        /*
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
         */
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

            log.InfoFormat("Cliente suscrito a FriendsManager. UserAccountId: {0}", userAccountId);
            return Task.CompletedTask;
        }

        public async Task<FriendRequestResult> SendFriendRequestAsync(string addresseeEmail)
        {
            int requesterUserAccountId = GetUserAccountIdFromCallback();
            if (requesterUserAccountId == 0)
            {
                log.Warn("SendFriendRequestAsync falló: No se pudo obtener UserAccountId del callback.");
                return FriendRequestResult.Failed; // Fallo de callback
            }

            try
            {
                log.InfoFormat("Iniciando SendFriendRequestAsync: RequesterId={0}, AddresseeEmail={1}", requesterUserAccountId, addresseeEmail);
                // --- CAMBIO: Lógica principal extraída ---
                return await TrySendFriendRequestAsync(requesterUserAccountId, addresseeEmail);
            }
            // --- CAMBIO: Manejo de Excepciones (sin helpers) ---
            catch (DbUpdateException dbUpEx)
            {
                log.Error($"DbUpdateException en SendFriendRequestAsync: RequesterId={requesterUserAccountId}, Email={addresseeEmail}", dbUpEx);
                var errorDetail = new ServiceErrorDetailDTO { ErrorCode = "DATABASE_ERROR", Message = "Error al guardar la solicitud de amistad." };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message));
            }
            catch (DbException dbEx)
            {
                log.Error($"DbException en SendFriendRequestAsync: RequesterId={requesterUserAccountId}, Email={addresseeEmail}", dbEx);
                var errorDetail = new ServiceErrorDetailDTO { ErrorCode = "DATABASE_ERROR", Message = "Error de base de datos al enviar la solicitud." };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message));
            }
            catch (Exception ex) // Captura errores de los helpers también
            {
                log.Fatal($"Error inesperado en SendFriendRequestAsync: RequesterId={requesterUserAccountId}, Email={addresseeEmail}", ex);
                var errorDetail = new ServiceErrorDetailDTO { ErrorCode = "UNEXPECTED_ERROR", Message = "Error inesperado al enviar la solicitud." };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message));
            }
        }
        /*
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
                return Task.FromResult(FriendRequestResult.CannotAddSelf);
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
        */

        public async Task<List<FriendDTO>> GetPendingRequestsAsync()
        {
            int userAccountId = GetUserAccountIdFromCallback();
            if (userAccountId == 0)
            {
                log.Warn("GetPendingRequestsAsync falló: No se pudo obtener UserAccountId del callback.");
                return new List<FriendDTO>(); // Lógica original
            }

            try
            {
                log.InfoFormat("Iniciando GetPendingRequestsAsync para UserAccountId: {0}", userAccountId);
                // --- CAMBIO: Llamada síncrona de repo en hilo de trabajo ---
                var requests = await Task.Run(() => friendshipRepository.GetPendingRequests(userAccountId));

                // Lógica de mapeo original
                var requestDTOs = requests.Select(req => new FriendDTO
                {
                    PlayerId = req.Player1.Id, // El ID del que envió la solicitud (Player1 es Requester)
                    Nickname = req.Player1.UserAccount.Nickname,
                }).ToList();

                log.InfoFormat("GetPendingRequestsAsync completado. {0} solicitudes encontradas.", requestDTOs.Count);
                return requestDTOs;
            }
            // --- CAMBIO: Manejo de Excepciones (sin helpers) ---
            catch (DbException dbEx)
            {
                log.Error($"DbException en GetPendingRequestsAsync (UserAccountId: {userAccountId})", dbEx);
                var errorDetail = new ServiceErrorDetailDTO { ErrorCode = "DATABASE_ERROR", Message = "Error al consultar las solicitudes pendientes." };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message));
            }
            catch (Exception ex)
            {
                log.Fatal($"Error inesperado en GetPendingRequestsAsync (UserAccountId: {userAccountId})", ex);
                var errorDetail = new ServiceErrorDetailDTO { ErrorCode = "UNEXPECTED_ERROR", Message = "Error inesperado al obtener solicitudes." };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message));
            }
        }

        /*
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
        */

        public async Task RespondToFriendRequestAsync(int requesterPlayerId, bool accepted)
        {
            int addresseeUserAccountId = GetUserAccountIdFromCallback();
            if (addresseeUserAccountId == 0)
            {
                log.Warn("RespondToFriendRequestAsync falló: No se pudo obtener UserAccountId del callback.");
                return; // Lógica original
            }

            try
            {
                log.InfoFormat("Iniciando RespondToFriendRequestAsync: AddresseeId={0}, RequesterId={1}, Accepted={2}", addresseeUserAccountId, requesterPlayerId, accepted);

                // --- CAMBIO: Llamada síncrona de repo en hilo de trabajo ---
                var addresseeAccount = await Task.Run(() => accountRepository.GetUserByUserAccountId(addresseeUserAccountId));

                if (addresseeAccount == null || !addresseeAccount.Player.Any())
                {
                    log.Warn($"RespondToFriendRequestAsync: No se encontró la cuenta o el jugador del destinatario (Id: {addresseeUserAccountId})");
                    return; // Lógica original
                }

                int addresseePlayerId = addresseeAccount.Player.First().Id;

                // --- CAMBIO: Llamada síncrona de repo en hilo de trabajo ---
                bool success = await Task.Run(() => friendshipRepository.RespondToFriendRequest(requesterPlayerId, addresseePlayerId, accepted));

                if (success && accepted)
                {
                    log.InfoFormat("Solicitud aceptada. Notificando... RequesterId={0}, AddresseeId={1}", requesterPlayerId, addresseePlayerId);
                    // --- CAMBIO: Lógica de notificación extraída ---
                    await NotifyOnRequestAcceptedAsync(requesterPlayerId, addresseeAccount);
                }
                else if (!success)
                {
                    log.WarnFormat("RespondToFriendRequestAsync falló (repositorio devolvió false). RequesterId={0}, AddresseeId={1}", requesterPlayerId, addresseePlayerId);
                }
            }
            // --- CAMBIO: Manejo de Excepciones (sin helpers) ---
            catch (DbUpdateException dbUpEx)
            {
                log.Error($"DbUpdateException en RespondToFriendRequestAsync: AddresseeId={addresseeUserAccountId}, RequesterId={requesterPlayerId}", dbUpEx);
                var errorDetail = new ServiceErrorDetailDTO { ErrorCode = "DATABASE_ERROR", Message = "Error al guardar la respuesta a la solicitud." };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message));
            }
            catch (DbException dbEx)
            {
                log.Error($"DbException en RespondToFriendRequestAsync: AddresseeId={addresseeUserAccountId}, RequesterId={requesterPlayerId}", dbEx);
                var errorDetail = new ServiceErrorDetailDTO { ErrorCode = "DATABASE_ERROR", Message = "Error de base de datos al responder a la solicitud." };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message));
            }
            catch (Exception ex)
            {
                log.Fatal($"Error inesperado en RespondToFriendRequestAsync: AddresseeId={addresseeUserAccountId}, RequesterId={requesterPlayerId}", ex);
                var errorDetail = new ServiceErrorDetailDTO { ErrorCode = "UNEXPECTED_ERROR", Message = "Error inesperado al responder a la solicitud." };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message));
            }
        }

        /*
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
        */

        public Task UnsubscribeFromFriendUpdatesAsync(int userAccountId)
        {
            connectedClients.TryRemove(userAccountId, out _);
            log.InfoFormat("Cliente desuscrito de FriendsManager. UserAccountId: {0}", userAccountId);
            
            return Task.CompletedTask;
        }

        private int GetUserAccountIdFromCallback()
        {
            var callback = operationContext.GetCallbackChannel<IFriendsCallback>();
            var entry = connectedClients.FirstOrDefault(pair => pair.Value == callback);
            
            return entry.Key;
        }

        private async Task<FriendRequestResult> TrySendFriendRequestAsync(int requesterUserAccountId, string addresseeEmail)
        {
            // Las llamadas al repo ocurren aquí, envueltas en Task.Run
            // El bloque catch en SendFriendRequestAsync las manejará
            var requesterAccount = await Task.Run(() => accountRepository.GetUserByUserAccountId(requesterUserAccountId));
            var addresseeAccount = await Task.Run(() => accountRepository.GetUserByEmail(addresseeEmail));

            if (addresseeAccount == null)
            {
                return FriendRequestResult.UserNotFound;
            }
            if (requesterAccount == null || !requesterAccount.Player.Any())
            {
                log.Warn($"TrySendFriendRequestAsync: No se encontró la cuenta o el jugador del solicitante (Id: {requesterUserAccountId})");
                return FriendRequestResult.Failed; // Fallo interno
            }
            if (requesterAccount.Id == addresseeAccount.Id)
            {
                return FriendRequestResult.CannotAddSelf;
            }
            if (!addresseeAccount.Player.Any())
            {
                log.Warn($"TrySendFriendRequestAsync: El destinatario no tiene un Player asociado (Email: {addresseeEmail})");
                return FriendRequestResult.UserNotFound; // Tratar como no encontrado
            }

            // Llamar al helper de validación (ahora async)
            var validationResult = await ValidateFriendRequestAsync(requesterAccount, addresseeAccount);

            if (validationResult != FriendRequestResult.Success)
            {
                return validationResult;
            }

            // Llamar al helper de creación y notificación (ahora async)
            bool success = await CreateAndNotifyFriendRequestAsync(requesterAccount, addresseeAccount);
            return success ? FriendRequestResult.Success : FriendRequestResult.Failed;
        }

        private async Task<FriendRequestResult> ValidateFriendRequestAsync(UserAccount requesterAccount, UserAccount addresseeAccount)
        {
            int requesterPlayerId = requesterAccount.Player.First().Id;
            int addresseePlayerId = addresseeAccount.Player.First().Id;

            // Comprobar si ya son amigos (usando la lógica de IDs de tu repo)
            var friends = await Task.Run(() => friendshipRepository.GetFriendsByUserAccountId(requesterAccount.Id));
            if (friends.Any(f => f.Player.Any(p => p.Id == addresseePlayerId)))
            {
                return FriendRequestResult.AlreadyFriends;
            }

            // Comprobar si (A -> B) ya está pendiente
            var addresseeRequests = await Task.Run(() => friendshipRepository.GetPendingRequests(addresseeAccount.Id));
            if (addresseeRequests.Any(req => req.RequesterId == requesterPlayerId))
            {
                return FriendRequestResult.RequestAlreadySent;
            }

            // Comprobar si (B -> A) ya fue recibida
            var requesterRequests = await Task.Run(() => friendshipRepository.GetPendingRequests(requesterAccount.Id));
            if (requesterRequests.Any(req => req.RequesterId == addresseePlayerId))
            {
                return FriendRequestResult.RequestAlreadyReceived;
            }

            return FriendRequestResult.Success;
        }

        /*
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
        */
        private async Task<bool> CreateAndNotifyFriendRequestAsync(UserAccount requesterAccount, UserAccount addresseeAccount)
        {
            int requesterPlayerId = requesterAccount.Player.First().Id;
            int addresseePlayerId = addresseeAccount.Player.First().Id;

            bool success = await Task.Run(() => friendshipRepository.CreateFriendRequest(requesterPlayerId, addresseePlayerId));

            if (!success)
            {
                return false;
            }

            // Notificar al destinatario si está conectado
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

        // --- NUEVO HELPER: Lógica de notificación para respuesta Aceptada ---
        private async Task NotifyOnRequestAcceptedAsync(int requesterPlayerId, UserAccount addresseeAccount)
        {
            var requesterAccount = await Task.Run(() => accountRepository.GetUserByPlayerId(requesterPlayerId));
            if (requesterAccount == null || !requesterAccount.Player.Any())
            {
                log.WarnFormat("NotifyOnRequestAcceptedAsync: No se encontró la cuenta o el jugador del solicitante (PlayerId: {0})", requesterPlayerId);
                return;
            }

            int addresseePlayerId = addresseeAccount.Player.First().Id;

            // Notificar al que aceptó (Addressee)
            if (connectedClients.TryGetValue(addresseeAccount.Id, out var addresseeCallback))
            {
                var requesterDto = new FriendDTO
                {
                    PlayerId = requesterPlayerId,
                    Nickname = requesterAccount.Nickname,
                };
                addresseeCallback.OnFriendAdded(requesterDto);
            }

            // Notificar al que envió (Requester)
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

        // --- NUEVO HELPER: Lógica de notificación para eliminación ---
        private async Task NotifyFriendRemovedAsync(int currentUserId, int friendToDeleteId)
        {
            var currentUserAccount = await Task.Run(() => accountRepository.GetUserByPlayerId(currentUserId));
            var friendAccount = await Task.Run(() => accountRepository.GetUserByPlayerId(friendToDeleteId));

            // Notificar al amigo eliminado (si está conectado y existe)
            if (friendAccount != null && connectedClients.TryGetValue(friendAccount.Id, out var friendCallback))
            {
                friendCallback.OnFriendRemoved(currentUserId);
            }

            // Notificar al usuario actual (que eliminó)
            if (currentUserAccount != null && connectedClients.TryGetValue(currentUserAccount.Id, out var currentCallback))
            {
                currentCallback.OnFriendRemoved(friendToDeleteId);
            }
        }

        /*
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
        */
    }
}
