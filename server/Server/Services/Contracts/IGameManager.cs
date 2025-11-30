using Services.Contracts.DTOs;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Contracts
{
    /// <summary>
    /// Manages live game flow and player interactions during a match session.
    /// </summary>
    [ServiceContract(CallbackContract =typeof(IGameManagerCallback))]
    public interface IGameManager
    {
        /// <summary>
        /// Creates an in-memory match associated with a waiting room and expected players.
        /// </summary>
        /// <param name="gameCode">The unique room/match identifier.</param>
        /// <param name="playersFromWaitingRoom">The four players that will take part in the match.</param>
        /// <returns>True if the match state was created; otherwise, false.</returns>
        bool CreateMatch(string gameCode, List<PlayerDTO> playersFromWaitingRoom);

        /// <summary>
        /// Subscribes a player to the match duplex callbacks.
        /// </summary>
        /// <param name="gameCode">The match identifier.</param>
        /// <param name="playerId">The player identifier.</param>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task SubscribeToMatchAsync(string gameCode, int playerId);

        /// <summary>
        /// Sends a clue from the clue-giver to their teammate.
        /// </summary>
        /// <param name="gameCode">The match identifier.</param>
        /// <param name="senderPlayerId">The sender (clue-giver) player id.</param>
        /// <param name="clue">The clue text.</param>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task SubmitClueAsync(string gameCode, int senderPlayerId, string clue);

        /// <summary>
        /// Sends a guess from the guesser.
        /// </summary>
        /// <param name="gameCode">The match identifier.</param>
        /// <param name="senderPlayerId">The sender (guesser) player id.</param>
        /// <param name="guess">The guess text.</param>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task SubmitGuessAsync(string gameCode, int senderPlayerId, string guess);

        /// <summary>
        /// Passes the current turn without scoring.
        /// </summary>
        /// <param name="gameCode">The match identifier.</param>
        /// <param name="senderPlayerId">The requesting player id.</param>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task PassTurnAsync(string gameCode, int senderPlayerId);

        /// <summary>
        /// Submits validation votes from the opposing team after a turn.
        /// </summary>
        /// <param name="gameCode">The match identifier.</param>
        /// <param name="senderPlayerId">The validator player id.</param>
        /// <param name="votes">The list of votes for each turn item.</param>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task SubmitValidationVotesAsync(string gameCode, int senderPlayerId, List<ValidationVoteDTO> votes);
    }

    /// <summary>
    /// Callback contract for real-time game events sent to clients.
    /// </summary>
    [ServiceContract]
    public interface IGameManagerCallback
    {
        /// <summary>
        /// Notifies that the match has been initialized and provides initial state.
        /// </summary>
        /// <param name="state">The initial state, including active players.</param>
        [OperationContract(IsOneWay = true)]
        void OnMatchInitialized(MatchInitStateDTO state);
        /// <summary>
        /// Notifies that a new round has started, providing the current round number and updated player roles.
        /// </summary>
        /// <param name="state">The initial state for the new round.</param>
        [OperationContract(IsOneWay = true)]
        void OnNewRoundStarted(RoundStartStateDTO state);
        /// <summary>
        /// Notifies clients about the remaining time in the current turn.
        /// </summary>
        /// <param name="secondsLeft">Seconds left in the turn.</param>
        [OperationContract(IsOneWay = true)]
        void OnTimerTick(int secondsLeft);

        /// <summary>
        /// Sends a new password/word to the clue-giver at the beginning of a turn.
        /// </summary>
        /// <param name="password">The secret word for the turn.</param>
        [OperationContract(IsOneWay = true)]
        void OnNewPassword(PasswordWordDTO password); // For the clue guy

        /// <summary>
        /// Delivers a clue to the guesser.
        /// </summary>
        /// <param name="clue">The clue message.</param>
        [OperationContract(IsOneWay = true)]
        void OnClueReceived(string clue); // For the guesser

        /// <summary>
        /// Announces whether a guess was correct and the updated team score.
        /// </summary>
        /// <param name="result">The guess result information.</param>
        [OperationContract(IsOneWay = true)]
        void OnGuessResult(GuessResultDTO result); // For both teams

        /// <summary>
        /// Starts the validation phase for validators and provides the turn history.
        /// </summary>
        /// <param name="turns">The list of turns to review and vote on.</param>
        [OperationContract(IsOneWay = true)]
        void OnBeginRoundValidation(List<TurnHistoryDTO> turns); // FOr the validators
        /// <summary>
        /// Notifies clients about the remaining time in the current validation phase.
        /// </summary>
        /// <param name="secondsLeft">Seconds left to vote.</param>
        [OperationContract(IsOneWay = true)]
        void OnValidationTimerTick(int secondsLeft);
        /// <summary>
        /// Sends the aggregated validation result and updated scores to players.
        /// </summary>
        /// <param name="result">The validation outcome, including penalties.</param>
        [OperationContract(IsOneWay = true)]
        void OnValidationComplete(ValidationResultDTO result);
        /// <summary>
        /// Notifies all players that a sudden death round has begun.
        /// </summary>
        [OperationContract(IsOneWay = true)]
        void OnSuddenDeathStarted();
        /// <summary>
        /// Notifies players that the match has ended and who the winner is.
        /// </summary>
        /// <param name="summary">Final scores and winner information.</param>
        [OperationContract(IsOneWay = true)]
        void OnMatchOver(MatchSummaryDTO summary);

        /// <summary>
        /// Cancels the match due to an error or disconnect and provides a reason.
        /// </summary>
        /// <param name="reason">The cancellation reason.</param>
        [OperationContract(IsOneWay = true)]
        void OnMatchCancelled(string reason); // For desync or errors
    }
}
