using PASSWORD_LIS_Client.ReportManagerServiceReference;
using System;
using System.ServiceModel;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.Services
{
    public interface IReportManagerService
    {
        event Action<string, string> ReportReceived;
        event Action<int> ReportCountUpdated;
        event Action<DateTime> PlayerBanned;
        event Action PlayerUnbanned;
        void SubscribeToReportUpdatesAsync(int playerId);
        Task<bool> SubmitReportAsync(ReportDTO reportDTO);
        Task<int> GetCurrentReportCountAsync(int playerId);
        Task<bool> IsPlayerBannedAsync(int playerId);
        void UnsubscribeFromReportUpdatesAsync(int playerId);
    }

    public class WcfReportManagerService : IReportManagerService, IReportManagerCallback
    {
        public event Action<string, string> ReportReceived;
        public event Action<int> ReportCountUpdated;
        public event Action<DateTime> PlayerBanned;
        public event Action PlayerUnbanned;

        private readonly IReportManager proxy;

        public WcfReportManagerService()
        {
            var context = new InstanceContext(this);
            var factory = new DuplexChannelFactory<IReportManager>(context, "NetTcpBinding_IReportManager");
            this.proxy = factory.CreateChannel();
        }

        public Task<int> GetCurrentReportCountAsync(int playerId)
        {
            return proxy.GetCurrentReportCountAsync(playerId);           
        }

        public Task<bool> IsPlayerBannedAsync(int playerId)
        {
            return proxy.IsPlayerBannedAsync(playerId);
        }

        public Task<bool> SubmitReportAsync(ReportDTO reportDTO)
        {
            return proxy.SubmitReportAsync(reportDTO);
        }

        public void SubscribeToReportUpdatesAsync(int playerId)
        {
            proxy.SubscribeToReportUpdatesAsync(playerId);
        }

        public void UnsubscribeFromReportUpdatesAsync(int playerId)
        {
            var channel = proxy as ICommunicationObject;
            try
            {
                if (channel == null || channel.State == CommunicationState.Faulted)
                {
                    channel?.Abort();
                }
                proxy.UnsubscribeFromReportUpdatesAsync(playerId);
            }
            catch (CommunicationException)
            {
                channel?.Abort();
            }
            catch (Exception)
            {
                channel?.Abort();
            }

        }

        public void OnPlayerBanned(DateTime banLiftTime)
        {
            PlayerBanned?.Invoke(banLiftTime);
        }

        public void OnPlayerUnbanned()
        {
            PlayerUnbanned?.Invoke();
        }

        public void OnReportCountUpdated(int newReportCount)
        {
            ReportCountUpdated?.Invoke(newReportCount);
        }

        public void OnReportReceived(string reporterNickname, string reason)
        {
            ReportReceived?.Invoke(reporterNickname, reason);
        }
    }
}
