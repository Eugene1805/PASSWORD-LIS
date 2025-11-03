using Moq;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.ViewModels;
using PASSWORD_LIS_Client.Views;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Test.ViewModelsTests
{
    public class LobbyViewModelTests
    {
        private static PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO LoggedUser(int playerId = 1, string email = "user@ex.com")
        {
            return new PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO
            {
                UserAccountId = 100,
                PlayerId = playerId,
                Email = email,
                Nickname = "nick",
                PhotoId = 0
            };
        }

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

        private static void SetAppSingleton<T>(string propertyName, T value)
        {
            var prop = typeof(PASSWORD_LIS_Client.App).GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public);
            var setter = prop?.GetSetMethod(true);
            setter?.Invoke(null, new object[] { value });
        }

        [Fact]
        public void CanJoinGame_ValidatesGameCodeLength()
        {
            var mockWindow = new Mock<IWindowService>();
            var mockFriends = new Mock<IFriendsManagerService>();
            var mockWaiting = new Mock<IWaitingRoomManagerService>();
            var mockReport = new Mock<IReportManagerService>();
            var vm = new LobbyViewModel(mockWindow.Object, mockFriends.Object, mockWaiting.Object, mockReport.Object);
            vm.GameCodeToJoin = "AB12"; // length4
            Assert.False(vm.JoinGameCommand.CanExecute(null));
            vm.GameCodeToJoin = "ABCDE"; // length5
            Assert.True(vm.JoinGameCommand.CanExecute(null));
        }

        [Fact]
        public async Task CreateGame_WhenBanned_ShouldShowWarningAndNotNavigate()
        {
            SessionManager.Login(LoggedUser());
            var mockWindow = new Mock<IWindowService>();
            var mockFriends = new Mock<IFriendsManagerService>();
            var mockWaiting = new Mock<IWaitingRoomManagerService>();
            var mockReport = new Mock<IReportManagerService>();
            mockReport.Setup(r => r.IsPlayerBannedAsync(It.IsAny<int>())).ReturnsAsync(true);
            var vm = new LobbyViewModel(mockWindow.Object, mockFriends.Object, mockWaiting.Object, mockReport.Object);

            await Task.Run(() => vm.CreateGameCommand.Execute(null));

            mockWindow.Verify(w => w.ShowPopUp(
            PASSWORD_LIS_Client.Properties.Langs.Lang.bannedAccountText,
            PASSWORD_LIS_Client.Properties.Langs.Lang.cantCreateMatchText,
            It.IsAny<PopUpIcon>()), Times.Once);
            Assert.DoesNotContain(mockWindow.Invocations, i => i.Method.Name == nameof(IWindowService.NavigateTo));
        }

        [Fact]
        public async Task CreateGame_OnSuccess_ShouldNavigateToWaitingRoom()
        {
            SessionManager.Login(LoggedUser());
            var mockWindow = new Mock<IWindowService>();
            var mockFriends = new Mock<IFriendsManagerService>();
            var mockWaiting = new Mock<IWaitingRoomManagerService>();
            var mockReport = new Mock<IReportManagerService>();

            // Provide App singletons used by LobbyViewModel -> WaitingRoomViewModel creation
            SetAppSingleton("WindowService", mockWindow.Object);
            SetAppSingleton("FriendsManagerService", mockFriends.Object);
            SetAppSingleton("WaitRoomManagerService", mockWaiting.Object);
            SetAppSingleton("ReportManagerService", mockReport.Object);

            mockReport.Setup(r => r.IsPlayerBannedAsync(It.IsAny<int>())).ReturnsAsync(false);
            mockWaiting.Setup(w => w.CreateGameAsync(It.IsAny<string>())).ReturnsAsync("ABCDE");

            var vm = new LobbyViewModel(mockWindow.Object, mockFriends.Object, mockWaiting.Object, mockReport.Object);

            await Task.Run(() => vm.CreateGameCommand.Execute(null));
            await WaitUntilAsync(() => mockWindow.Invocations.Any(i => i.Method.Name == nameof(IWindowService.NavigateTo)));

            Assert.Contains(mockWindow.Invocations, i => i.Method.Name == nameof(IWindowService.NavigateTo));
        }

        [Fact]
        public async Task JoinGame_AsGuestBannedCheckNotRequired_SuccessNavigates()
        {
            SessionManager.Login(LoggedUser(-1)); // Guest user
            var mockWindow = new Mock<IWindowService>();
            var mockFriends = new Mock<IFriendsManagerService>();
            var mockWaiting = new Mock<IWaitingRoomManagerService>();
            var mockReport = new Mock<IReportManagerService>();

            // Provide App singletons used by LobbyViewModel -> WaitingRoomViewModel creation
            SetAppSingleton("WindowService", mockWindow.Object);
            SetAppSingleton("FriendsManagerService", mockFriends.Object);
            SetAppSingleton("WaitRoomManagerService", mockWaiting.Object);
            SetAppSingleton("ReportManagerService", mockReport.Object);

            mockWaiting.Setup(w => w.JoinGameAsGuestAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            var vm = new LobbyViewModel(mockWindow.Object, mockFriends.Object, mockWaiting.Object, mockReport.Object)
            {
                GameCodeToJoin = "ABCDE"
            };

            await Task.Run(() => vm.JoinGameCommand.Execute(null));
            await WaitUntilAsync(() => mockWindow.Invocations.Any(i => i.Method.Name == nameof(IWindowService.NavigateTo)));

            Assert.Contains(mockWindow.Invocations, i => i.Method.Name == nameof(IWindowService.NavigateTo));
        }

        [Fact]
        public async Task JoinGame_AsRegisteredBanned_ShouldWarnAndNotNavigate()
        {
            SessionManager.Login(LoggedUser(5));
            var mockWindow = new Mock<IWindowService>();
            var mockFriends = new Mock<IFriendsManagerService>();
            var mockWaiting = new Mock<IWaitingRoomManagerService>();
            var mockReport = new Mock<IReportManagerService>();
            mockReport.Setup(r => r.IsPlayerBannedAsync(It.IsAny<int>())).ReturnsAsync(true);

            var vm = new LobbyViewModel(mockWindow.Object, mockFriends.Object, mockWaiting.Object, mockReport.Object)
            {
                GameCodeToJoin = "ABCDE"
            };

            await Task.Run(() => vm.JoinGameCommand.Execute(null));

            mockWindow.Verify(w => w.ShowPopUp(
            PASSWORD_LIS_Client.Properties.Langs.Lang.bannedAccountText,
            PASSWORD_LIS_Client.Properties.Langs.Lang.cantJoinMatchText,
            It.IsAny<PopUpIcon>()), Times.Once);
            Assert.DoesNotContain(mockWindow.Invocations, i => i.Method.Name == nameof(IWindowService.NavigateTo));
        }
    }
}
