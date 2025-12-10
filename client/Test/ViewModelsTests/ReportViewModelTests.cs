using Moq;
using PASSWORD_LIS_Client.ReportManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.ViewModels;
using PASSWORD_LIS_Client.Views;
using PASSWORD_LIS_Client.WaitingRoomManagerServiceReference;
using Xunit;

namespace Test.ViewModelsTests
{
    public class ReportViewModelTests
    {
        private static PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO MakeUser(int playerId)
        {
            return new PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO
            {
                PlayerId = playerId,
                Nickname = "reporter"
            };
        }

        private static PlayerDTO MakePlayer(int id, string nick)
        {
            return new PlayerDTO { Id = id, Nickname = nick };
        }

        [Fact]
        public void SubmitReport_WhenSuccess_ShouldShowSuccessAndClose()
        {
            var reporter = MakeUser(10);
            var reported = MakePlayer(20, "badguy");
            var mockWindow = new Mock<IWindowService>();
            var mockService = new Mock<IReportManagerService>();
            mockService.Setup(s => s.SubmitReportAsync(It.IsAny<ReportDTO>())).ReturnsAsync(true);
            var vm = new ReportViewModel(reporter, reported, mockWindow.Object, mockService.Object)
            {
                ReportReason = "spam"
            };

            vm.SubmitReportCommand.Execute(null);

            mockWindow.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.reportSummitedText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.thanksForReportText,
                It.IsAny<PopUpIcon>()), Times.Once);
            mockWindow.Verify(w => w.CloseWindow(vm), Times.Once);
        }

        [Fact]
        public void SubmitReport_WhenFails_ShouldShowErrorAndClose()
        {
            var reporter = MakeUser(10);
            var reported = MakePlayer(20, "badguy");
            var mockWindow = new Mock<IWindowService>();
            var mockService = new Mock<IReportManagerService>();
            mockService.Setup(s => s.SubmitReportAsync(It.IsAny<ReportDTO>())).ReturnsAsync(false);
            var vm = new ReportViewModel(reporter, reported, mockWindow.Object, mockService.Object)
            {
                ReportReason = "spam"
            };

            vm.SubmitReportCommand.Execute(null);

            mockWindow.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.errorTitleText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.couldNotSummitReportText,
                It.IsAny<PopUpIcon>()), Times.Once);
            mockWindow.Verify(w => w.CloseWindow(vm), Times.Once);
        }
    }
}
