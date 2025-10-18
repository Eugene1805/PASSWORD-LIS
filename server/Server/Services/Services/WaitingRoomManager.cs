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
        private class ConnectedPlayer
        {
            public IWaitingRoomCallback CallbackChannel { get; set; }
            public PlayerDTO PlayerData { get; set; }
        }

        private readonly ConcurrentDictionary<int, ConnectedPlayer> connectedClients;
        private readonly IPlayerRepository repository;
        private readonly IOperationContextWrapper operationContext;
        private static int _guestIdCounter = 0;

        public WaitingRoomManager(IPlayerRepository playerRepository, IOperationContextWrapper operationContextWrapper)
        {
            this.connectedClients = new ConcurrentDictionary<int, ConnectedPlayer>();
            this.repository = playerRepository;
            this.operationContext = operationContextWrapper;
        }

        public async Task<bool> JoinAsRegisteredPlayerAsync(string username)
        {
            Player playerEntity = repository.GetPlayerByUsername(username); 
            if (playerEntity == null || connectedClients.ContainsKey(playerEntity.Id))
            {
                return false;
            }

            var playerDto = new PlayerDTO
            {
                Id = playerEntity.Id,
                Username = playerEntity.UserAccount.Nickname,
                Role = await AssignRoleAsync(),
                IsReady = false
            };

            return await AddPlayerToRoomAsync(playerDto);
        }

        public async Task<bool> JoinAsGuestAsync(string guestUsername)
        {
            if (connectedClients.Values.Any(p => p.PlayerData.Username.Equals(guestUsername, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            int guestId = Interlocked.Decrement(ref _guestIdCounter);

            var playerDto = new PlayerDTO
            {
                Id = guestId,
                Username = guestUsername,
                Role = await AssignRoleAsync(),
                IsReady = false
            };

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

            Console.WriteLine($"Player {playerDto.Username} (ID: {playerDto.Id}) joined as {playerDto.Role}.");
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
            return this.connectedClients.Values.Select(p => p.PlayerData).ToList();
        }

        public async Task SendMessageAsync(ChatMessage message)
        {
            Console.WriteLine($"Message from {message.SenderUsername}: {message.Message}");
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
                    // Si falla la comunicación, asumimos que el cliente se desconectó
                    Console.WriteLine($"Error broadcasting to player ID {player.Key}. Removing. Error: {ex.Message}");
                    disconnectedPlayerIds.Add(player.Key);
                }
            }

            // Limpiar clientes desconectados
            foreach (var id in disconnectedPlayerIds)
            {
                await LeaveRoomAsync(id);
            }
        }

        private async Task<string> AssignRoleAsync()
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
