using Moq;
using Services.Services;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Data.DAL.Interfaces;
using Data.Model;
using System.ServiceModel;
using Services.Wrappers;
using Services.Contracts;

namespace Test.ServicesTests
{
    public class ReportManagerTests
    {
        private readonly Mock<IReportRepository> reportRepository;
        private readonly Mock<IPlayerRepository> playerRepository;
        private readonly Mock<IBanRepository> banRepository;
        private readonly Mock<IOperationContextWrapper> contexWrapper;
        private readonly Mock<IReportManagerCallback> callback;
        private readonly ReportManager sut;

        public ReportManagerTests()
        {
            reportRepository = new Mock<IReportRepository>();
            playerRepository = new Mock<IPlayerRepository>();
            banRepository = new Mock<IBanRepository>();
            contexWrapper = new Mock<IOperationContextWrapper>();
            callback = new Mock<IReportManagerCallback>();

            contexWrapper.Setup(c => c.GetCallbackChannel<IReportManagerCallback>()).Returns(callback.Object);

            sut = new ReportManager(
                reportRepository.Object, playerRepository.Object, banRepository.Object, contexWrapper.Object);
        }

        private static Player MakePlayer(int id, string nick)
        {
            return new Player
            {
                Id = id,
                UserAccount = new UserAccount { Nickname = nick }
            };
        }

        [Fact]
        public async Task SubmitReportAsync_ShouldThrow_WhenPayloadInvalid()
        {
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(() => sut.SubmitReportAsync(null));

            var bad = new ReportDTO 
            { 
                ReporterPlayerId = 0, ReportedPlayerId = 2 
            };
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(() => sut.SubmitReportAsync(bad));

            var self = new ReportDTO 
            { 
                ReporterPlayerId = 5, ReportedPlayerId = 5 
            };
            await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(() => sut.SubmitReportAsync(self));
        }

        [Fact]
        public async Task SubmitReportAsync_ShouldThrow_WhenReporterNotFound()
        {
            var dto = new ReportDTO 
            { 
                ReporterPlayerId = 1, ReportedPlayerId = 2, Reason = "x" 
            };
            playerRepository.Setup(r => r.GetPlayerByIdAsync(1)).ReturnsAsync((Player?)null);
            playerRepository.Setup(r => r.GetPlayerByIdAsync(2)).ReturnsAsync(MakePlayer(2, "Reported"));

            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(() => sut.SubmitReportAsync(dto));
            Assert.Equal(ServiceErrorCode.ReporterNotFound, ex.Detail.Code);
        }

        [Fact]
        public async Task SubmitReportAsync_ShouldThrow_WhenReportedNotFound()
        {
            var dto = new ReportDTO 
            { 
                ReporterPlayerId = 1, ReportedPlayerId = 2, Reason = "x" 
            };
            playerRepository.Setup(r => r.GetPlayerByIdAsync(1)).ReturnsAsync(MakePlayer(1, "Reporter"));
            playerRepository.Setup(r => r.GetPlayerByIdAsync(2)).ReturnsAsync((Player?)null);

            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(() => sut.SubmitReportAsync(dto));
            Assert.Equal(ServiceErrorCode.ReportedPlayerNotFound, ex.Detail.Code);
        }

        [Fact]
        public async Task SubmitReportAsync_ShouldThrow_WhenDuplicateSinceLastBan()
        {
            var dto = new ReportDTO 
            { 
                ReporterPlayerId = 1, ReportedPlayerId = 2, Reason = "dup" 
            };
            playerRepository.Setup(r => r.GetPlayerByIdAsync(1)).ReturnsAsync(MakePlayer(1, "Reporter"));
            playerRepository.Setup(r => r.GetPlayerByIdAsync(2)).ReturnsAsync(MakePlayer(2, "Reported"));
            banRepository.Setup(b => b.GetLastBanEndTimeAsync(2)).ReturnsAsync((DateTime?)null);
            reportRepository.Setup(r => r.HasReporterReportedSinceAsync(1, 2, null)).ReturnsAsync(true);

            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(() => sut.SubmitReportAsync(dto));
            Assert.Equal(ServiceErrorCode.MaxOneReportPerBan, ex.Detail.Code);
        }

        [Fact]
        public async Task SubmitReportAsync_ShouldAddReport_AndNotifyCallback_WhenValid()
        {
            var dto = new ReportDTO 
            { 
                ReporterPlayerId = 1, ReportedPlayerId = 2, Reason = "ok" 
            };
            playerRepository.Setup(r => r.GetPlayerByIdAsync(1)).ReturnsAsync(MakePlayer(1, "RepNick"));
            playerRepository.Setup(r => r.GetPlayerByIdAsync(2)).ReturnsAsync(MakePlayer(2, "TargetNick"));
            banRepository.Setup(b => b.GetLastBanEndTimeAsync(2)).ReturnsAsync((DateTime?)null);
            reportRepository.Setup(r => r.HasReporterReportedSinceAsync(1, 2, null)).ReturnsAsync(false);
            reportRepository.Setup(r => r.AddReportAsync(It.IsAny<Report>())).Returns(Task.CompletedTask);
            reportRepository.Setup(r => r.GetReportCountForPlayerSinceAsync(2, null)).ReturnsAsync(1);

            sut.SubscribeToReportUpdatesAsync(2);
            var result = await sut.SubmitReportAsync(dto);

            Assert.True(result);
            reportRepository.Verify(r => r.AddReportAsync(It.Is<Report>(rep 
                => rep.ReporterPlayerId == 1 && rep.ReportedPlayerId == 2 && rep.Reason == "ok")), Times.Once);
            callback.Verify(c => c.OnReportReceived("RepNick", "ok"), Times.Once);
            callback.Verify(c => c.OnReportCountUpdated(1), Times.Once);
        }

        [Fact]
        public async Task SubmitReportAsync_ShouldBanPlayer_WhenThresholdReached()
        {
            var dto = new ReportDTO 
            { 
                ReporterPlayerId = 3, ReportedPlayerId = 4, Reason = "ban" 
            };
            playerRepository.Setup(r => r.GetPlayerByIdAsync(3)).ReturnsAsync(MakePlayer(3, "R"));
            playerRepository.Setup(r => r.GetPlayerByIdAsync(4)).ReturnsAsync(MakePlayer(4, "T"));
            banRepository.Setup(b => b.GetLastBanEndTimeAsync(4)).ReturnsAsync((DateTime?)null);
            reportRepository.Setup(r => r.HasReporterReportedSinceAsync(3, 4, null)).ReturnsAsync(false);
            reportRepository.Setup(r => r.AddReportAsync(It.IsAny<Report>())).Returns(Task.CompletedTask);
            
            reportRepository.Setup(r => r.GetReportCountForPlayerSinceAsync(4, null)).ReturnsAsync(3);
            banRepository.Setup(b => b.GetActiveBanForPlayerAsync(4)).ReturnsAsync((Ban?)null);
            banRepository.Setup(b => b.AddBanAsync(It.IsAny<Ban>())).Returns(Task.CompletedTask);

            sut.SubscribeToReportUpdatesAsync(4);
            var result = await sut.SubmitReportAsync(dto);

            Assert.True(result);
            banRepository.Verify(b => b.AddBanAsync(It.Is<Ban>(bn => bn.PlayerId == 4)), Times.Once);
            callback.Verify(c => c.OnPlayerBanned(It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        public async Task GetCurrentReportCountAsync_ShouldUseSinceLastBan()
        {
            var since = DateTime.UtcNow.AddMinutes(-30);
            banRepository.Setup(b => b.GetLastBanEndTimeAsync(10)).ReturnsAsync(since);
            reportRepository.Setup(r => r.GetReportCountForPlayerSinceAsync(10, since)).ReturnsAsync(5);

            var count = await sut.GetCurrentReportCountAsync(10);
            Assert.Equal(5, count);
        }

        [Fact]
        public async Task IsPlayerBannedAsync_ShouldReturnTrue_WhenActiveBanExists()
        {
            banRepository.Setup(b => b.GetActiveBanForPlayerAsync(15)).ReturnsAsync(
                new Ban 
                { 
                    PlayerId = 15, EndTime = DateTime.UtcNow.AddMinutes(10) 
                });
            var res = await sut.IsPlayerBannedAsync(15);
            Assert.True(res);
        }
    }
}
