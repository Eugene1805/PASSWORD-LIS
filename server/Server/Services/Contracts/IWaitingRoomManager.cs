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
        bool JoinAsRegisteredPlayer(string username);

        [OperationContract]
        bool JoinAsGuest(string guestUsername);

        [OperationContract]
        void LeaveRoom(int playerId);

        [OperationContract]
        void SendMessage(ChatMessage message);

        [OperationContract]
        List<PlayerDTO> GetConnectedPlayers();
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
