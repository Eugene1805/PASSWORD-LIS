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
        Task<string> CreateGameAsync(string email);

        [OperationContract]
        Task<bool> JoinGameAsRegisteredPlayerAsync(string gameCode, string email);
        [OperationContract]
        Task<bool> JoinGameAsGuestAsync(string gameCode, string nickname);

        [OperationContract]
        Task SendMessageAsync(string gameCode, ChatMessageDTO message);

        [OperationContract]
        Task LeaveGameAsync(string gameCode, int playerId);

        [OperationContract]
        Task StartGameAsync(string gameCode);
        [OperationContract]
        Task<List<PlayerDTO>> GetPlayersInGameAsync(string gameCode);
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
        void OnGameCreated(string gameCode);

        [OperationContract(IsOneWay = true)]
        void OnGameStarted();
    }
}
