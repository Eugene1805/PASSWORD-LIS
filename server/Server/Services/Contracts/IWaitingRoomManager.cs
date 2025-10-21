using Services.Contracts.DTOs;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Contracts
{
    [ServiceContract(CallbackContract = typeof(IWaitingRoomCallback))]
    public interface IWaitingRoomManager
    {
        [OperationContract]
        Task<bool> JoinAsRegisteredPlayerAsync(string email);

        [OperationContract]
        Task<bool> JoinAsGuestAsync(string guestNickname);

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
