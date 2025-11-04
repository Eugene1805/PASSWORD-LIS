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
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task<string> CreateRoomAsync(string email);

        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task<int> JoinRoomAsRegisteredPlayerAsync(string gameCode, string email);

        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task<bool> JoinRoomAsGuestAsync(string gameCode, string nickname);

        [OperationContract]
        Task SendMessageAsync(string gameCode, ChatMessageDTO message);

        [OperationContract]
        Task LeaveRoomAsync(string gameCode, int playerId);

        [OperationContract]
        Task StartGameAsync(string gameCode);
        [OperationContract]
        Task<List<PlayerDTO>> GetPlayersInRoomAsync(string gameCode);
        [OperationContract]
        Task HostLeftAsync(string gameCode);
    }
    public interface IWaitingRoomCallback
    {
        [OperationContract(IsOneWay = true)]
        void OnPlayerJoined(PlayerDTO player);

        [OperationContract(IsOneWay = true)]
        void OnPlayerLeft(int playerId);

        [OperationContract(IsOneWay = true)]
        void OnMessageReceived(ChatMessageDTO message);

        [OperationContract(IsOneWay = true)]
        void OnGameStarted();
        [OperationContract(IsOneWay = true)]
        void OnHostLeft();
    }
}
