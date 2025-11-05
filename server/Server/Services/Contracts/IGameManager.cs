using Services.Contracts.DTOs;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Contracts
{
    [ServiceContract(CallbackContract =typeof(IGameManagerCallback))]
    public interface IGameManager
    {
        bool CreateMatch(string gameCode, List<PlayerDTO> playersFromWaitingRoom);
        [OperationContract]
        Task SubscribeToMatchAsync(string gameCode, int playerId);

        [OperationContract]
        Task SubmitClueAsync(string gameCode, int senderPlayerId, string clue);

        [OperationContract]
        Task SubmitGuessAsync(string gameCode, int senderPlayerId, string guess);

        [OperationContract]
        Task PassTurnAsync(string gameCode, int senderPlayerId);

        [OperationContract]
        Task SubmitValidationVotesAsync(string gameCode, int senderPlayerId, List<ValidationVoteDTO> votes);
    }

    public interface IGameManagerCallback
    {
        [OperationContract(IsOneWay = true)]
        void OnMatchInitialized(MatchInitStateDTO state);

        [OperationContract(IsOneWay = true)]
        void OnTimerTick(int secondsLeft);

        [OperationContract(IsOneWay = true)]
        void OnNewPassword(PasswordWordDTO password); // For the clue guy

        [OperationContract(IsOneWay = true)]
        void OnClueReceived(string clue); // For the guesser

        [OperationContract(IsOneWay = true)]
        void OnGuessResult(GuessResultDTO result); // For both teams

        [OperationContract(IsOneWay = true)]
        void OnBeginRoundValidation(List<TurnHistoryDTO> turns); // FOr the validators

        [OperationContract(IsOneWay = true)]
        void OnMatchOver(MatchSummaryDTO summary);

        [OperationContract(IsOneWay = true)]
        void OnMatchCancelled(string reason); // For desync or errors
    }
}
