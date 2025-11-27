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
    public class ReportManager : ServiceBase, IReportManager
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
            IBanRepository banRepository,IOperationContextWrapper operationContext):base(log)
        {
            this.reportRepository = reportRepository;
            this.playerRepository = playerRepository;
            this.banRepository = banRepository;
            this.operationContext = operationContext;
            connectedClients = new ConcurrentDictionary<int, (IReportManagerCallback, Timer)>();
        }

        public async Task<bool> IsPlayerBannedAsync(int playerId)
        {
            return await ExecuteAsync(async () =>
            {
                var activeBan = await banRepository.GetActiveBanForPlayerAsync(playerId);
                return activeBan != null;
            }, context:"ReportManager: IsPlayerBannedAsync");            
        }

        public async Task<bool> SubmitReportAsync(ReportDTO reportDTO)
        {
            return await ExecuteAsync(async () =>
            {
                ValidateReportPayload(reportDTO);

                var players = await GetAndValidatePlayersAsync(reportDTO.ReporterPlayerId, reportDTO.ReportedPlayerId);

                var since = await banRepository.GetLastBanEndTimeAsync(reportDTO.ReportedPlayerId);

                await EnsureNotAlreadyReportedAsync(reportDTO.ReporterPlayerId, reportDTO.ReportedPlayerId, since);

                await AddReportAndLogAsync(reportDTO);

                var totalReports = await reportRepository.GetReportCountForPlayerSinceAsync(reportDTO.ReportedPlayerId,
                    since);
                log.InfoFormat("Reported player {0} now has {1} report(s) since last ban.", reportDTO.ReportedPlayerId,
                    totalReports);

                NotifyReportCallbacks(reportDTO.ReportedPlayerId, players.Reporter.UserAccount.Nickname, 
                    reportDTO.Reason, totalReports);

                await EvaluateBanIfThresholdReachedAsync(reportDTO.ReportedPlayerId, totalReports);

                return true;
            }, context: "ReportManager: SubmitReportAsync");
        }

        private async Task BanPlayerAsync(int playerId)
        {
            await ExecuteAsync(async () =>
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
            }, context: "ReportManager: BanPlayerAsync");
        }

        public async void SubscribeToReportUpdatesAsync(int playerId)
        {
            await ExecuteAsync(() =>
            {
                var callbackChannel = operationContext.GetCallbackChannel<IReportManagerCallback>();
                connectedClients[playerId] = (callbackChannel, null);
                log.InfoFormat("Player {0} subscribed to report updates.", playerId);

                if (callbackChannel is ICommunicationObject commObject)
                {
                    commObject.Faulted += (s, e) => UnsubscribeFromReportUpdatesAsync(playerId);
                    commObject.Closed += (s, e) =>  UnsubscribeFromReportUpdatesAsync(playerId);
                }
                return Task.CompletedTask;
            }, context: "ReportManager: SubscribeToReportUpdatesAsync");            
        }

        public async void UnsubscribeFromReportUpdatesAsync(int playerId)
        {
            await ExecuteAsync(() =>
            {
                if (connectedClients.TryRemove(playerId, out var client))
                {
                    client.BanTimer?.Dispose();
                    log.InfoFormat("Player {0} unsubscribed from report updates.", playerId);
                }
                return Task.CompletedTask;
            }, context: "ReportManager: UnsubscribeFromReportUpdatesAsync");            
        }

        public async Task<int> GetCurrentReportCountAsync(int playerId)
        {
            return await ExecuteAsync(async () =>
            {
                var since = await banRepository.GetLastBanEndTimeAsync(playerId);
                return await reportRepository.GetReportCountForPlayerSinceAsync(playerId, since);
            }, context: "ReportManager: GetCurrentReportCountAsync");
        }

        private void NotifyPlayerUnbanned(int playerId)
        {
            if (connectedClients.TryGetValue(playerId, out var client))
            {
                client.Callback.OnPlayerUnbanned();
                client.BanTimer?.Dispose();
                connectedClients[playerId] = (client.Callback, null);
                log.InfoFormat("Player {0} unbanned and timer disposed.", playerId);
            }
        }

        private static void ValidateReportPayload(ReportDTO reportDTO)
        {
            if (reportDTO == null || reportDTO.ReporterPlayerId <= 0 || reportDTO.ReportedPlayerId <= 0
                || reportDTO.ReporterPlayerId == reportDTO.ReportedPlayerId)
            {
                throw FaultExceptionFactory.Create(ServiceErrorCode.InvalidReportPayload,
                    "INVALID_REPORT_PAYLOAD", "Invalid report payload.");
            }
        }

        private async Task<(Player Reporter, Player Reported)> GetAndValidatePlayersAsync(int reporterId,
            int reportedId)
        {
            var reporter = await playerRepository.GetPlayerByIdAsync(reporterId);
            if (reporter == null || reporter.Id <= 0)
            {
                throw FaultExceptionFactory.Create(ServiceErrorCode.ReporterNotFound,
                    "REPORTER_NOT_FOUND", "Reporter player not found.");
            }
            var reported = await playerRepository.GetPlayerByIdAsync(reportedId);
            if (reported == null || reported.Id <= 0)
            {
                throw FaultExceptionFactory.Create(ServiceErrorCode.ReportedPlayerNotFound,
                    "REPORTED_PLAYER_NOT_FOUND", "Reported player not found.");
            }
            return (reporter, reported);
        }

        private async Task EnsureNotAlreadyReportedAsync(int reporterId, int reportedId, DateTime? since)
        {
            var alreadyReported = await reportRepository.HasReporterReportedSinceAsync(reporterId, reportedId, since);
            if (alreadyReported)
            {
                throw FaultExceptionFactory.Create(ServiceErrorCode.InvalidReportPayload,
                    "INVALID_REPORT_PAYLOAD",
                    "You have already reported this player. You can report again after they are banned.");
            }
        }

        private async Task AddReportAndLogAsync(ReportDTO reportDTO)
        {
            var newReport = new Report
            {
                ReporterPlayerId = reportDTO.ReporterPlayerId,
                ReportedPlayerId = reportDTO.ReportedPlayerId,
                Reason = reportDTO.Reason,
                CreatedAt = DateTime.UtcNow
            };
            await reportRepository.AddReportAsync(newReport);
            log.InfoFormat("Report submitted: reporter={0}, reported={1}.", reportDTO.ReporterPlayerId,
                reportDTO.ReportedPlayerId);
        }

        private void NotifyReportCallbacks(int reportedPlayerId, string reporterNickname,
            string reason, int totalReports)
        {
            if (connectedClients.TryGetValue(reportedPlayerId, out var client))
            {
                client.Callback.OnReportReceived(reporterNickname, reason);
                client.Callback.OnReportCountUpdated(totalReports);
            }
        }

        private async Task EvaluateBanIfThresholdReachedAsync(int reportedPlayerId, int totalReports)
        {
            if (totalReports >= ReportsForBan)
            {
                if (await IsPlayerBannedAsync(reportedPlayerId))
                {
                    log.WarnFormat("Player {0} already banned; skipping ban.", reportedPlayerId);
                }
                else
                {
                    await BanPlayerAsync(reportedPlayerId);
                }
            }
        }
    }
}