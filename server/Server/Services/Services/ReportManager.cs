using Data.DAL.Interfaces;
using Data.Model;
using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Services.Wrappers;
using System;
using System.Collections.Concurrent;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Services.Util;

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
        private static readonly ILog log = LogManager.GetLogger(typeof(ReportManager));

        public ReportManager(IReportRepository reportRepository, IPlayerRepository playerRepository,
            IBanRepository banRepository,IOperationContextWrapper operationContext)
        {
            this.reportRepository = reportRepository;
            this.playerRepository = playerRepository;
            this.banRepository = banRepository;
            this.operationContext = operationContext;
            connectedClients = new ConcurrentDictionary<int, (IReportManagerCallback, Timer)>();
        }

        public async Task<bool> IsPlayerBannedAsync(int playerId)
        {
            try
            {
                var activeBan = await banRepository.GetActiveBanForPlayerAsync(playerId);
                return activeBan != null;
            }
            catch (Exception ex)
            {
                log.Error("Error checking ban status.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError,
                    "UNEXPECTED_ERROR", "Error checking ban status.");
            }
        }

        public async Task<bool> SubmitReportAsync(ReportDTO reportDTO)
        {
            if (reportDTO == null || reportDTO.ReporterPlayerId <= 0 || reportDTO.ReportedPlayerId <= 0)
            {
                throw FaultExceptionFactory.Create(ServiceErrorCode.InvalidReportPayload, 
                    "INVALID_REPORT_PAYLOAD", "Invalid report payload.");
            }

            try
            {
                var newReport = new Report
                {
                    ReporterPlayerId = reportDTO.ReporterPlayerId,
                    ReportedPlayerId = reportDTO.ReportedPlayerId,
                    Reason = reportDTO.Reason
                };
                await reportRepository.AddReportAsync(newReport);
                log.InfoFormat("Report submitted: reporter={0}, reported={1}.", reportDTO.ReporterPlayerId,
                    reportDTO.ReportedPlayerId);

                var totalReports = await reportRepository.GetReportCountForPlayerAsync(reportDTO.ReportedPlayerId);
                log.InfoFormat("Reported player {0} now has {1} report(s).", reportDTO.ReportedPlayerId, totalReports);

                if (connectedClients.TryGetValue(reportDTO.ReportedPlayerId, out var client))
                {
                    var reporter = await playerRepository.GetPlayerByIdAsync(reportDTO.ReporterPlayerId);
                    if (reporter == null || reporter.Id <= 0)
                    {
                        throw FaultExceptionFactory.Create(ServiceErrorCode.ReporterNotFound,
                            "REPORTER_NOT_FOUND", "Reporter player not found.");
                    }
                    client.Callback.OnReportReceived(reporter.UserAccount.Nickname, reportDTO.Reason);
                    client.Callback.OnReportCountUpdated(totalReports);
                }

                if (totalReports >= ReportsForBan)
                {
                    if (await IsPlayerBannedAsync(reportDTO.ReportedPlayerId))
                    {
                        log.WarnFormat("Player {0} already banned; skipping ban.", reportDTO.ReportedPlayerId);
                    }
                    else
                    {
                        await BanPlayerAsync(reportDTO.ReportedPlayerId);
                    }
                }

                return true;
            }
            catch (FaultException<ServiceErrorDetailDTO>)
            {
                throw;
            }
            catch (Exception ex)
            {
                log.Error("Error while submitting report.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError, 
                    "UNEXPECTED_ERROR", "Unexpected error while submitting report.");
            }
        }

        private async Task BanPlayerAsync(int playerId)
        {
            try
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
                log.InfoFormat("Player {0} banned until {1:O}.", playerId, endTime);

                if (connectedClients.TryGetValue(playerId, out var client))
                {
                    client.Callback.OnPlayerBanned(endTime);

                    var banTimer = new Timer(_ => NotifyPlayerUnbanned(playerId), null, 
                        BanDuration, Timeout.InfiniteTimeSpan);
                    connectedClients[playerId] = (client.Callback, banTimer);
                }
            }
            catch (Exception ex)
            {
                log.Error("Error banning player.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.BanPersistenceError, 
                    "BAN_PERSISTENCE_ERROR", "Failed to persist ban information.");
            }
        }

        private void NotifyPlayerUnbanned(int playerId)
        {
            try
            {
                if (connectedClients.TryGetValue(playerId, out var client))
                {
                    client.Callback.OnPlayerUnbanned();
                    client.BanTimer?.Dispose();
                    connectedClients[playerId] = (client.Callback, null);
                    log.InfoFormat("Player {0} unbanned and timer disposed.", playerId);
                }
            }
            catch (Exception ex)
            {
                log.Error("Error notifying player unbanned.", ex);
            }
        }

        public Task SubscribeToReportUpdatesAsync(int playerId)
        {
            try
            {
                var callbackChannel = operationContext.GetCallbackChannel<IReportManagerCallback>();
                connectedClients[playerId] = (callbackChannel, null);
                log.InfoFormat("Player {0} subscribed to report updates.", playerId);

                var commObject = (ICommunicationObject)callbackChannel;
                commObject.Faulted += (s, e) => UnsubscribeFromReportUpdatesAsync(playerId);
                commObject.Closed += (s, e) => UnsubscribeFromReportUpdatesAsync(playerId);
            }
            catch (Exception ex)
            {
                log.Error("Error subscribing to report updates.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.SubscriptionError, 
                    "SUBSCRIPTION_ERROR", "Failed to subscribe player to report updates.");
            }
            return Task.CompletedTask;
        }

        public Task UnsubscribeFromReportUpdatesAsync(int playerId)
        {
            try
            {
                if (connectedClients.TryRemove(playerId, out var client))
                {
                    client.BanTimer?.Dispose();
                    log.InfoFormat("Player {0} unsubscribed from report updates.", playerId);
                }
            }
            catch (Exception ex)
            {
                log.Error("Error unsubscribing from report updates.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UnsubscriptionError,
                    "UNSUBSCRIPTION_ERROR", "Failed to unsubscribe player from report updates.");
            }
            return Task.CompletedTask;
        }

        public async Task<int> GetCurrentReportCountAsync(int playerId)
        {
            try
            {
                return await reportRepository.GetReportCountForPlayerAsync(playerId);
            }
            catch (Exception ex)
            {
                log.Error("Error getting current report count.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError, 
                    "UNEXPECTED_ERROR", "Error retrieving report count.");
            }
        }
    }
}