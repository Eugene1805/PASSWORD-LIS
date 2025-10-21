using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.WaitingRoomManagerServiceReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.Services
{
    public interface IWaitingRoomManagerService
    {
        Task<bool> JoinAsRegisteredPlayerAsync(string email);
        Task<bool> JoinAsGuestAsync(string guestNickname);
        Task LeaveRoomAsync(int playerId);
        Task<List<PlayerDTO>> GetConnectedPlayersAsync();
        Task SendMessageAsync(string message);

    }

    public class WcfWaitingRoomManagerService : IWaitingRoomManagerService, IWaitingRoomManagerCallback
    {
        public event Action<ChatMessage> MessageReceived;
        public event Action<PlayerDTO> PlayerJoined;
        public event Action<int> PlayerLeft;

        private readonly IWaitingRoomManager proxy;
        public WcfWaitingRoomManagerService() 
        {
            var context = new InstanceContext(this);
            var factory = new DuplexChannelFactory<IWaitingRoomManager>(context, "NetTcpBinding_IWaitingRoomManager");
            proxy = factory.CreateChannel();
        }

        public async Task<List<PlayerDTO>> GetConnectedPlayersAsync()
        {
            var playersArray = await proxy.GetConnectedPlayersAsync();
            return playersArray.ToList();
        }

        public async Task<bool> JoinAsGuestAsync(string guestNickname)
        {
            try
            {
                await proxy.JoinAsGuestAsync(guestNickname);
                return true;
            }
            catch (Exception)
            {
                Console.WriteLine("Error al unirse como invitado");
                throw;
            }
        }

        public async Task<bool> JoinAsRegisteredPlayerAsync(string email)
        {
            try
            {
                return await proxy.JoinAsRegisteredPlayerAsync(email);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al unirse: {ex.Message}");
                throw;
            }
        }

        public async Task LeaveRoomAsync(int playerId)
        {
            await proxy.LeaveRoomAsync(playerId);
        }
        public async Task SendMessageAsync(string message)
        {
            var chatMessage = new ChatMessage
            {
                SenderNickname = SessionManager.CurrentUser.Nickname,
                Message = message
            };
            await proxy.SendMessageAsync(chatMessage);
        }


        public void OnMessageReceived(ChatMessage message)
        {
            MessageReceived?.Invoke(message);
        }

        public void OnPlayerJoined(PlayerDTO player)
        {
            PlayerJoined?.Invoke(player);
        }

        public void OnPlayerLeft(int playerId)
        {
            PlayerLeft?.Invoke(playerId);
        }
    }
}
