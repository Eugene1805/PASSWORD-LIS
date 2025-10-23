using Data.DAL.Interfaces;
using Data.Model;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Wrappers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class WaitingRoomManager : IWaitingRoomManager
    {
        sealed class ConnectedPlayer
        {
            public IWaitingRoomCallback CallbackChannel { get; set; }
            public PlayerDTO PlayerData { get; set; }
        }

        private readonly ConcurrentDictionary<int, ConnectedPlayer> connectedClients;
        private readonly IPlayerRepository repository;
        private readonly IOperationContextWrapper operationContext;
        private static int guestIdCounter = 0;

        public WaitingRoomManager(IPlayerRepository playerRepository, IOperationContextWrapper operationContextWrapper)
        {
            this.connectedClients = new ConcurrentDictionary<int, ConnectedPlayer>();
            this.repository = playerRepository;
            this.operationContext = operationContextWrapper;
        }

        public async Task<bool> JoinAsRegisteredPlayerAsync(string email)
        {
            Player playerEntity = repository.GetPlayerByEmail(email); 
            if (playerEntity == null || connectedClients.ContainsKey(playerEntity.Id))
            {
                return false;
            }

            var playerDto = new PlayerDTO
            {
                Id = playerEntity.Id,
                PhotoId = playerEntity.UserAccount.PhotoId ?? 0,
                Nickname = playerEntity.UserAccount.Nickname,
                Role = AssignRole(),
                IsReady = false
            };

            return await AddPlayerToRoomAsync(playerDto);
        }

        public async Task<bool> JoinAsGuestAsync(string guestNickname)
        {
            Console.WriteLine($"[DEBUG] Intento de unirse como invitado: '{guestNickname}'");
            if (connectedClients.Values.Any(p => p.PlayerData.Nickname.Equals(guestNickname, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"[DEBUG] INGRESO RECHAZADO: El nombre de usuario '{guestNickname}' ya existe en la sala.");
                return false;
            }

            int guestId = Interlocked.Decrement(ref guestIdCounter);

            var playerDto = new PlayerDTO
            {
                Id = guestId,
                Nickname = guestNickname,
                Role = AssignRole(),
                IsReady = false
            };
            Console.WriteLine($"[DEBUG] '{guestNickname}' (ID:{guestId}) fue validado y será añadido a la sala.");
            Console.WriteLine($"Player {guestNickname} (ID:{guestId}) joined as {playerDto.Role}");
            return await AddPlayerToRoomAsync(playerDto);
        }

        private async Task<bool> AddPlayerToRoomAsync(PlayerDTO playerDto)
        {
            var callback = operationContext.GetCallbackChannel<IWaitingRoomCallback>();
            var newPlayer = new ConnectedPlayer
            {
                CallbackChannel = callback,
                PlayerData = playerDto
            };

            if (!this.connectedClients.TryAdd(playerDto.Id, newPlayer))
            {
                return false;
            }

            Console.WriteLine($"Player {playerDto.Nickname} (ID: {playerDto.Id}) joined as {playerDto.Role}.");
            await BroadcastAsync(client => client.CallbackChannel.OnPlayerJoined(playerDto));
            return true;
        }

        public async Task LeaveRoomAsync(int playerId)
        {
            if (this.connectedClients.TryRemove(playerId, out _))
            {
                Console.WriteLine($"Player with ID {playerId} left.");
                await BroadcastAsync(client => client.CallbackChannel.OnPlayerLeft(playerId));
            }
        }

        public async Task<List<PlayerDTO>> GetConnectedPlayersAsync()
        {
            return await Task.Run(()=> connectedClients.Values.Select(p => p.PlayerData).ToList());
        }

        public async Task SendMessageAsync(ChatMessage message)
        {
            Console.WriteLine($"Message from {message.SenderNickname}: {message.Message}");
            await BroadcastAsync(client => client.CallbackChannel.OnMessageReceived(message));
        }

        private async Task BroadcastAsync(Action<ConnectedPlayer> action)
        {
            List<int> disconnectedPlayerIds = new List<int>();

            foreach (var player in this.connectedClients)
            {
                try
                {
                    action(player.Value);
                }
                catch (Exception ex)
                {
                    // If there's an error, assume the client is disconnected
                    Console.WriteLine($"Error broadcasting to player ID {player.Key}. Removing. Error: {ex.Message}");
                    disconnectedPlayerIds.Add(player.Key);
                }
            }

            foreach (var id in disconnectedPlayerIds)
            {
                await LeaveRoomAsync(id);
            }
        }

        private string AssignRole()
        {
            var rolesInUse = connectedClients.Values.Select(p => p.PlayerData.Role).ToList();

            int clueGuyCount = rolesInUse.Count(r => r == "ClueGuy");
            int guesserCount = rolesInUse.Count(r => r == "Guesser");

            if (clueGuyCount < 2)
            {
                return "ClueGuy";
            }
            if (guesserCount < 2)
            {
                return "Guesser";
            }

            return "Observer";
        }
    }
}
