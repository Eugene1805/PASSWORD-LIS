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
        private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000, int pollMs = 25)
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
        public async Task Ctor_ShouldLoadTopTeamsOnSuccess()
        {
            // Arrange
            var mockService = new Mock<ITopPlayersManagerService>();
            var mockWindow = new Mock<IWindowService>();
            var data = new[]
            {
 new TeamDTO { Score =10, PlayersNicknames = new[] { "a", "b" } },
 new TeamDTO { Score =5, PlayersNicknames = new[] { "c", "d" } },
 };
            mockService.Setup(s => s.GetTopAsync(It.IsAny<int>())).ReturnsAsync(data);

            // Act
            var vm = new TopPlayersViewModel(mockService.Object, mockWindow.Object);

            // Assert
            await WaitUntilAsync(() => vm.IsLoading == false);
            Assert.Equal(2, vm.TopTeams.Count);
            Assert.All(vm.TopTeams, t => Assert.IsType<TeamDTO>(t));
        }

        [Fact]
        public async Task Ctor_WhenServiceThrowsFault_ShouldShowServerErrorPopup()
        {
            // Arrange
            var mockService = new Mock<ITopPlayersManagerService>();
            var mockWindow = new Mock<IWindowService>();
            mockService
            .Setup(s => s.GetTopAsync(It.IsAny<int>()))
            .ThrowsAsync(new FaultException<ServiceErrorDetailDTO>(new ServiceErrorDetailDTO()));

            // Act
            var vm = new TopPlayersViewModel(mockService.Object, mockWindow.Object);

            // Assert
            await WaitUntilAsync(() => vm.IsLoading == false);
            mockWindow.Verify(w => w.ShowPopUp(
            PASSWORD_LIS_Client.Properties.Langs.Lang.errorTitleText,
            PASSWORD_LIS_Client.Properties.Langs.Lang.unexpectedServerErrorText,
            It.IsAny<PopUpIcon>()), Times.Once);
        }

        [Fact]
        public async Task Ctor_WhenEndpointNotFound_ShouldShowConnectionWarning()
        {
            // Arrange
            var mockService = new Mock<ITopPlayersManagerService>();
            var mockWindow = new Mock<IWindowService>();
            mockService
            .Setup(s => s.GetTopAsync(It.IsAny<int>()))
            .ThrowsAsync(new EndpointNotFoundException());

            // Act
            var vm = new TopPlayersViewModel(mockService.Object, mockWindow.Object);

            // Assert
            await WaitUntilAsync(() => vm.IsLoading == false);
            mockWindow.Verify(w => w.ShowPopUp(
            PASSWORD_LIS_Client.Properties.Langs.Lang.connectionErrorTitleText,
            PASSWORD_LIS_Client.Properties.Langs.Lang.serverConnectionInternetErrorText,
            It.IsAny<PopUpIcon>()), Times.Once);
        }

        [Fact]
        public async Task Ctor_WhenCommunicationException_ShouldShowNetworkError()
        {
            // Arrange
            var mockService = new Mock<ITopPlayersManagerService>();
            var mockWindow = new Mock<IWindowService>();
            mockService
            .Setup(s => s.GetTopAsync(It.IsAny<int>()))
            .ThrowsAsync(new CommunicationException());

            // Act
            var vm = new TopPlayersViewModel(mockService.Object, mockWindow.Object);

            // Assert
            await WaitUntilAsync(() => vm.IsLoading == false);
            mockWindow.Verify(w => w.ShowPopUp(
            PASSWORD_LIS_Client.Properties.Langs.Lang.networkErrorTitleText,
            PASSWORD_LIS_Client.Properties.Langs.Lang.serverCommunicationErrorText,
            It.IsAny<PopUpIcon>()), Times.Once);
        }
    }
}
