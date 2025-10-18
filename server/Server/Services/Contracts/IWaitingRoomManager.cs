using Services.Contracts.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Services.Contracts
{
    [ServiceContract(CallbackContract = typeof(IWaitingRoomCallback))]
    public interface IWaitingRoomManager
    {
        [OperationContract]
        Task<bool> JoinAsRegisteredPlayerAsync(string username);

        [OperationContract]
        Task<bool> JoinAsGuestAsync(string guestUsername);

        [OperationContract]
        Task LeaveRoomAsync(int playerId);

        [OperationContract]
        Task SendMessageAsync(ChatMessage message);

        [OperationContract]
        Task<List<PlayerDTO>> GetConnectedPlayersAsync();
    }
    public interface IWaitingRoomCallback
    {
        [OperationContract(IsOneWay = true)]
        void OnPlayerJoined(PlayerDTO player);

        [OperationContract(IsOneWay = true)]
        void OnPlayerLeft(int playerId);

        [OperationContract(IsOneWay = true)]
        void OnMessageReceived(ChatMessage message);
    }
}
