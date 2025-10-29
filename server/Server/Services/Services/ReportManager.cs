using Data.DAL.Interfaces;
using Data.Model;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Wrappers;
using System;
using System.Collections.Concurrent;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class ReportManager : IReportManager
    {
        private const int ReportsForBan = 3;
        private static readonly TimeSpan BanDuration = TimeSpan.FromHours(1);

        private readonly ConcurrentDictionary<int, (IReportManagerCallback Callback, Timer BanTimer)> connectedClients;
        private readonly IReportRepository reportRepository;
        private readonly IPlayerRepository playerRepository;
        private readonly IBanRepository banRepository;
        private readonly IOperationContextWrapper operationContext;

        public ReportManager(IReportRepository reportRepository, IPlayerRepository playerRepository, IBanRepository banRepository,
            IOperationContextWrapper operationContext)
        {
            this.reportRepository = reportRepository;
            this.playerRepository = playerRepository;
            this.banRepository = banRepository;
            this.operationContext = operationContext;
            connectedClients = new ConcurrentDictionary<int, (IReportManagerCallback, Timer)>();
        }

        public async Task<bool> IsPlayerBannedAsync(int playerId)
        {
            var activeBan = await banRepository.GetActiveBanForPlayerAsync(playerId);
            return activeBan != null;
        }

        public async Task<bool> SubmitReportAsync(ReportDTO reportDTO)
        {
            try
            {
                var newReport = new Report
                {
                    ReporterPlayerId = reportDTO.ReporterPlayerId,
                    ReportedPlayerId = reportDTO.ReportedPlayerId,
                    Reason = reportDTO.Reason
                };
                await reportRepository.AddReportAsync(newReport);

                var totalReports = await reportRepository.GetReportCountForPlayerAsync(reportDTO.ReportedPlayerId);

                if (connectedClients.TryGetValue(reportDTO.ReportedPlayerId, out var client))
                {
                    var reporter = await playerRepository.GetPlayerByIdAsync(reportDTO.ReporterPlayerId);
                    client.Callback.OnReportReceived(reporter.UserAccount.Nickname, reportDTO.Reason);
                    client.Callback.OnReportCountUpdated(totalReports);
                }

                if (totalReports >= ReportsForBan && !await IsPlayerBannedAsync(reportDTO.ReportedPlayerId))
                {
                    await BanPlayerAsync(reportDTO.ReportedPlayerId);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task BanPlayerAsync(int playerId)
        {
            var startTime = DateTime.UtcNow;
            var endTime = startTime.Add(BanDuration);

            var newBan = new Ban
            {
                PlayerId = playerId,
                StartTime = startTime,
                EndTime = endTime
            };
            await banRepository.AddBanAsync(newBan);

            if (connectedClients.TryGetValue(playerId, out var client))
            {
                client.Callback.OnPlayerBanned(endTime);

                var banTimer = new Timer(_ => NotifyPlayerUnbanned(playerId), null, BanDuration, Timeout.InfiniteTimeSpan);
                connectedClients[playerId] = (client.Callback, banTimer);
            }
        }

        private void NotifyPlayerUnbanned(int playerId)
        {
            if (connectedClients.TryGetValue(playerId, out var client))
            {
                client.Callback.OnPlayerUnbanned();
                client.BanTimer?.Dispose();
                connectedClients[playerId] = (client.Callback, null);
            }
        }

        public Task SubscribeToReportUpdatesAsync(int playerId)
        {
            var callbackChannel = operationContext.GetCallbackChannel<IReportManagerCallback>();
            connectedClients[playerId] = (callbackChannel, null);

            var commObject = (ICommunicationObject)callbackChannel;
            commObject.Faulted += (s, e) => UnsubscribeFromReportUpdatesAsync(playerId);
            commObject.Closed += (s, e) => UnsubscribeFromReportUpdatesAsync(playerId);

            return Task.CompletedTask;
        }
        public Task UnsubscribeFromReportUpdatesAsync(int playerId)
        {
            if (connectedClients.TryRemove(playerId, out var client))
            {
                client.BanTimer?.Dispose();
            }
            return Task.CompletedTask;
        }
        public async Task<int> GetCurrentReportCountAsync(int playerId)
        {
            return await reportRepository.GetReportCountForPlayerAsync(playerId);
        }
    }
}