using PASSWORD_LIS_Client.WaitingRoomManagerServiceReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.Services
{
    public interface IWaitingRoomManagerService
    {
        event Action<ChatMessageDTO> MessageReceived;
        event Action<PlayerDTO> PlayerJoined;
        event Action<int> PlayerLeft;
        event Action<string> GameCreated;
        event Action GameStarted;
        Task<string> CreateGameAsync(string email);
        Task<bool> JoinGameAsRegisteredPlayerAsync(string gameCode, string email);
        Task<bool> JoinGameAsGuestAsync(string gameCode, string guestNickname);
        Task LeaveGameAsync(string gameCode, int playerId);
        Task StartGameAsync(string gameCode);
        Task SendMessageAsync(string gameCode, ChatMessageDTO message);
        Task<List<PlayerDTO>> GetPlayersInGameAsync(string gameCode);

    }

    public class WcfWaitingRoomManagerService : IWaitingRoomManagerService, IWaitingRoomManagerCallback
    {
        public event Action<ChatMessageDTO> MessageReceived;
        public event Action<PlayerDTO> PlayerJoined;
        public event Action<int> PlayerLeft;
        public event Action<string> GameCreated;
        public event Action GameStarted;

        private readonly IWaitingRoomManager proxy;
        public WcfWaitingRoomManagerService() 
        {
            var context = new InstanceContext(this);
            var factory = new DuplexChannelFactory<IWaitingRoomManager>(context, "NetTcpBinding_IWaitingRoomManager");
            proxy = factory.CreateChannel();
        }

        public async Task<string> CreateGameAsync(string email)
        {
            return await proxy.CreateGameAsync(email);
        }

        public async Task<bool> JoinGameAsRegisteredPlayerAsync(string gameCode, string email)
        {
            return await proxy.JoinGameAsRegisteredPlayerAsync(gameCode, email);
        }

        public async Task<bool> JoinGameAsGuestAsync(string gameCode, string guestNickname)
        {
            return await proxy.JoinGameAsGuestAsync(gameCode, guestNickname);
        }

        public async Task LeaveGameAsync(string gameCode, int playerId)
        {
            await proxy.LeaveGameAsync(gameCode, playerId);
        }

        public async Task StartGameAsync(string gameCode)
        {
            await proxy.StartGameAsync(gameCode);
        }

        public async Task SendMessageAsync(string gameCode, ChatMessageDTO message)
        {
            await proxy.SendMessageAsync(gameCode, message);
        }

        public async Task<List<PlayerDTO>> GetPlayersInGameAsync(string gameCode)
        {
            var playersArray = await proxy.GetPlayersInGameAsync(gameCode);
            return playersArray?.ToList() ?? new List<PlayerDTO>();
        }

        public void OnPlayerJoined(PlayerDTO player)
        {
            PlayerJoined?.Invoke(player);
        }

        public void OnPlayerLeft(int playerId)
        {
            PlayerLeft?.Invoke(playerId);
        }

        public void OnMessageReceived(ChatMessageDTO message)
        {
            MessageReceived?.Invoke(message);
        }

        public void OnGameCreated(string gameCode)
        {
            GameCreated?.Invoke(gameCode);
        }

        public void OnGameStarted()
        {
            GameStarted?.Invoke();
        }

    }
}
