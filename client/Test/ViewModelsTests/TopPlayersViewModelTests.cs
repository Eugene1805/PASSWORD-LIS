using Moq;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.TopPlayersManagerServiceReference;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.ViewModels;
using PASSWORD_LIS_Client.Views;
using System;
using System.ServiceModel;
using System.Threading.Tasks;
using Xunit;

namespace Test.ViewModelsTests
{
    public class TopPlayersViewModelTests
    {
        private static async Task WaitUntilLoadingCompletes(Func<bool> condition, int timeoutMs = 2000, int pollMs = 25)
        {
            var start = DateTime.UtcNow;
            while (!condition())
            {
                if ((DateTime.UtcNow - start).TotalMilliseconds > timeoutMs)
                {
                    throw new TimeoutException("Condition not met in time");
                }
                await Task.Delay(pollMs).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task Constructor_WhenServiceReturnsData_ShouldLoadTopTeamsSuccessfully()
        {
            var mockService = new Mock<ITopPlayersManagerService>();
            var mockWindow = new Mock<IWindowService>();
            var expectedTeams = new[]
            {
                new TeamDTO { Score = 10, PlayersNicknames = new[] { "a", "b" } },
                new TeamDTO { Score = 5, PlayersNicknames = new[] { "c", "d" } },
            };
            mockService.Setup(s => s.GetTopAsync(It.IsAny<int>())).ReturnsAsync(expectedTeams);

            var viewModel = new TopPlayersViewModel(mockService.Object, mockWindow.Object);

            await WaitUntilLoadingCompletes(() => !viewModel.IsLoading);
            Assert.Equal(2, viewModel.TopTeams.Count);
            Assert.All(viewModel.TopTeams, team => Assert.IsType<TeamDTO>(team));
        }

        [Fact]
        public async Task Constructor_WhenServiceThrowsFaultException_ShouldShowStatisticsErrorPopup()
        {
            var mockService = new Mock<ITopPlayersManagerService>();
            var mockWindow = new Mock<IWindowService>();
            
            var errorDetail = new ServiceErrorDetailDTO
            {
                Code = ServiceErrorCode.STATISTICS_ERROR,
                ErrorCode = "STATISTICS_ERROR"
            };
            mockService
                .Setup(s => s.GetTopAsync(It.IsAny<int>()))
                .ThrowsAsync(new FaultException<ServiceErrorDetailDTO>(errorDetail));

            var viewModel = new TopPlayersViewModel(mockService.Object, mockWindow.Object);

            await WaitUntilLoadingCompletes(() => !viewModel.IsLoading);
            mockWindow.Verify(w => w.ShowPopUp(
                It.Is<string>(title => title == PASSWORD_LIS_Client.Properties.Langs.Lang.errorTitleText),
                It.Is<string>(message => message == PASSWORD_LIS_Client.Properties.Langs.Lang.statisticsErrorText),
                PopUpIcon.Error), Times.Once);
        }

        [Fact]
        public async Task Constructor_WhenEndpointNotFound_ShouldShowConnectionErrorPopup()
        {
            var mockService = new Mock<ITopPlayersManagerService>();
            var mockWindow = new Mock<IWindowService>();
            mockService
                .Setup(s => s.GetTopAsync(It.IsAny<int>()))
                .ThrowsAsync(new EndpointNotFoundException());

            var viewModel = new TopPlayersViewModel(mockService.Object, mockWindow.Object);

            await WaitUntilLoadingCompletes(() => !viewModel.IsLoading);
            mockWindow.Verify(w => w.ShowPopUp(
                It.Is<string>(title => title == PASSWORD_LIS_Client.Properties.Langs.Lang.connectionErrorTitleText),
                It.Is<string>(message => message == PASSWORD_LIS_Client.Properties.Langs.Lang.serverConnectionInternetErrorText),
                PopUpIcon.Error), Times.Once);
        }

        [Fact]
        public async Task Constructor_WhenCommunicationException_ShouldShowNetworkErrorPopup()
        {
            var mockService = new Mock<ITopPlayersManagerService>();
            var mockWindow = new Mock<IWindowService>();
            mockService
                .Setup(s => s.GetTopAsync(It.IsAny<int>()))
                .ThrowsAsync(new CommunicationException());

            var viewModel = new TopPlayersViewModel(mockService.Object, mockWindow.Object);

            await WaitUntilLoadingCompletes(() => !viewModel.IsLoading);
            mockWindow.Verify(w => w.ShowPopUp(
                It.Is<string>(title => title == PASSWORD_LIS_Client.Properties.Langs.Lang.networkErrorTitleText),
                It.Is<string>(message => message == PASSWORD_LIS_Client.Properties.Langs.Lang.serverCommunicationErrorText),
                PopUpIcon.Error), Times.Once);
        }
    }
}
