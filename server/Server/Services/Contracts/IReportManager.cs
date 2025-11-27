using Services.Contracts.DTOs;
using System;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Contracts
{
    /// <summary>
    /// Exposes reporting operations and a callback for receiving report-related notifications.
    /// </summary>
    [ServiceContract(CallbackContract = typeof(IReportManagerCallback))]
    public interface IReportManager
    {
        /// <summary>
        /// Subscribes the specified player to receive report updates via callbacks.
        /// </summary>
        /// <param name="playerId">The player identifier.</param>
        [OperationContract(IsOneWay = true)]
        void SubscribeToReportUpdatesAsync(int playerId);

        /// <summary>
        /// Submits a new report.
        /// </summary>
        /// <param name="reportDTO">The report payload.</param>
        /// <returns>True if the report was successfully recorded; otherwise, false.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task<bool> SubmitReportAsync(ReportDTO reportDTO);

        /// <summary>
        /// Gets the current report count for a player.
        /// </summary>
        /// <param name="playerId">The player identifier.</param>
        /// <returns>The number of reports currently registered for the player.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task<int> GetCurrentReportCountAsync(int playerId);

        /// <summary>
        /// Checks whether the specified player is currently banned.
        /// </summary>
        /// <param name="playerId">The player identifier.</param>
        /// <returns>True if banned; otherwise, false.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task<bool> IsPlayerBannedAsync(int playerId);

        /// <summary>
        /// Unsubscribes the specified player from report updates.
        /// </summary>
        /// <param name="playerId">The player identifier.</param>
        [OperationContract(IsOneWay = true)]
        void UnsubscribeFromReportUpdatesAsync(int playerId);
    }

    /// <summary>
    /// Callback contract for receiving report status updates.
    /// </summary>
    [ServiceContract]
    public interface IReportManagerCallback
    {
        /// <summary>
        /// Notifies that a new report has been received.
        /// </summary>
        /// <param name="reporterNickname">The nickname of the reporter.</param>
        /// <param name="reason">The reason provided in the report.</param>
        [OperationContract(IsOneWay = true)]
        void OnReportReceived(string reporterNickname, string reason);

        /// <summary>
        /// Notifies that the report count for the player has changed.
        /// </summary>
        /// <param name="newReportCount">The updated report count.</param>
        [OperationContract(IsOneWay = true)]
        void OnReportCountUpdated(int newReportCount);

        /// <summary>
        /// Notifies that the player has been banned.
        /// </summary>
        /// <param name="banLiftTime">The date and time when the ban will be lifted.</param>
        [OperationContract(IsOneWay = true)]
        void OnPlayerBanned(DateTime banLiftTime);

        /// <summary>
        /// Notifies that the player has been unbanned.
        /// </summary>
        [OperationContract(IsOneWay = true)]
        void OnPlayerUnbanned();
    }
}
