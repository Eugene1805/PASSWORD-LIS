using Services.Contracts.DTOs;
using System;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Contracts
{
    [ServiceContract(CallbackContract = typeof(IReportManagerCallback))]
    public interface IReportManager
    {
        [OperationContract(IsOneWay = true)]
        Task SubscribeToReportUpdatesAsync(int playerId);
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task<bool> SubmitReportAsync(ReportDTO reportDTO);
        [OperationContract]
        Task<int> GetCurrentReportCountAsync(int playerId);

        [OperationContract]
        Task<bool> IsPlayerBannedAsync(int playerId);

        [OperationContract(IsOneWay = true)]
        Task UnsubscribeFromReportUpdatesAsync(int playerId);
    }
    public interface IReportManagerCallback
    {
        [OperationContract(IsOneWay = true)]
        void OnReportReceived(string reporterNickname, string reason);

        [OperationContract(IsOneWay = true)]
        void OnReportCountUpdated(int newReportCount);

        [OperationContract(IsOneWay = true)]
        void OnPlayerBanned(DateTime banLiftTime);

        [OperationContract(IsOneWay = true)]
        void OnPlayerUnbanned();
    }
}
