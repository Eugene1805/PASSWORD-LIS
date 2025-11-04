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
        event Action GameStarted;
        event Action HostLeft;
        Task<string> CreateRoomAsync(string email);
        Task<int> JoinRoomAsRegisteredPlayerAsync(string gameCode, string email);
        Task<bool> JoinRoomAsGuestAsync(string gameCode, string guestNickname);
        Task LeaveRoomAsync(string gameCode, int playerId);
        Task StartGameAsync(string gameCode);
        Task SendMessageAsync(string gameCode, ChatMessageDTO message);
        Task<List<PlayerDTO>> GetPlayersInRoomAsync(string gameCode);
        Task HostLeftAsync(string gameCode);

    }
    // TODO: Add exception handling and abort the resources
    public class WcfWaitingRoomManagerService : IWaitingRoomManagerService, IWaitingRoomManagerCallback
    {
        public event Action<ChatMessageDTO> MessageReceived;
        public event Action<PlayerDTO> PlayerJoined;
        public event Action<int> PlayerLeft;
        public event Action GameStarted;
        public event Action HostLeft;

        private readonly IWaitingRoomManager proxy;
        public WcfWaitingRoomManagerService() 
        {
            var context = new InstanceContext(this);
            var factory = new DuplexChannelFactory<IWaitingRoomManager>(context, "NetTcpBinding_IWaitingRoomManager");
            proxy = factory.CreateChannel();
        }

        public async Task<string> CreateRoomAsync(string email)
        {
            return await proxy.CreateRoomAsync(email);
        }

        public async Task<int> JoinRoomAsRegisteredPlayerAsync(string gameCode, string email)
        {
            return await proxy.JoinRoomAsRegisteredPlayerAsync(gameCode, email);
        }

        public async Task<bool> JoinRoomAsGuestAsync(string gameCode, string guestNickname)
        {
            return await proxy.JoinRoomAsGuestAsync(gameCode, guestNickname);
        }

        public async Task LeaveRoomAsync(string gameCode, int playerId)
        {
            await proxy.LeaveRoomAsync(gameCode, playerId);
        }

        public async Task StartGameAsync(string gameCode)
        {
            await proxy.StartGameAsync(gameCode);
        }

        public async Task SendMessageAsync(string gameCode, ChatMessageDTO message)
        {
            await proxy.SendMessageAsync(gameCode, message);
        }

        public async Task<List<PlayerDTO>> GetPlayersInRoomAsync(string gameCode)
        {
            var playersArray = await proxy.GetPlayersInRoomAsync(gameCode);
            return playersArray?.ToList() ?? new List<PlayerDTO>();
        }
        public async Task HostLeftAsync(string gameCode)
        {
            await proxy.HostLeftAsync(gameCode);
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

        public void OnGameStarted()
        {
            GameStarted?.Invoke();
        }

        public void OnHostLeft()
        {
            HostLeft?.Invoke();
        }

    }
}
