using Moq;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.ViewModels;
using Xunit;

namespace Test.ViewModelsTests
{
    public class SettingsViewModelTests
    {
        [Fact]
        public void Ctor_ShouldInitializeLanguageSelection_FromSettings()
        {
            PASSWORD_LIS_Client.Properties.Settings.Default.languageCode = "en-US";
            var mockWindow = new Mock<IWindowService>();
            var mockFriends = new Mock<IFriendsManagerService>();
            var mockMusic = new Mock<BackgroundMusicService>();

            var vm = new SettingsViewModel(mockWindow.Object, mockFriends.Object, mockMusic.Object);

            Assert.True(vm.IsEnglishSelected);
            Assert.False(vm.IsSpanishSelected);
        }

        [Fact]
        public void MusicVolume_Setter_ShouldUpdateViewModelValue()
        {
            var mockWindow = new Mock<IWindowService>();
            var mockFriends = new Mock<IFriendsManagerService>();
            var mockMusic = new Mock<BackgroundMusicService>();
            double setValue =0.7;
            var vm = new SettingsViewModel(mockWindow.Object, mockFriends.Object, mockMusic.Object);

            vm.MusicVolume = setValue;

            Assert.Equal(setValue, vm.MusicVolume,3);
        }

        [Fact]
        public void LanguageSelection_SetEnglish_ShouldUpdateSettingsAndProvider()
        {
            var mockWindow = new Mock<IWindowService>();
            var mockFriends = new Mock<IFriendsManagerService>();
            var mockMusic = new Mock<BackgroundMusicService>();
            var vm = new SettingsViewModel(mockWindow.Object, mockFriends.Object, mockMusic.Object);

            vm.IsEnglishSelected = true;

            Assert.Equal("en-US", PASSWORD_LIS_Client.Properties.Settings.Default.languageCode);
        }

        [Fact]
        public void LanguageSelection_SetSpanish_ShouldUpdateSettingsAndProvider()
        {
            var mockWindow = new Mock<IWindowService>();
            var mockFriends = new Mock<IFriendsManagerService>();
            var mockMusic = new Mock<BackgroundMusicService>();
            var vm = new SettingsViewModel(mockWindow.Object, mockFriends.Object, mockMusic.Object);

            vm.IsSpanishSelected = true;

            Assert.Equal("es-MX", PASSWORD_LIS_Client.Properties.Settings.Default.languageCode);
        }

        [Fact]
        public void Logout_WhenLoggedInRegisteredUser_ShouldUnsubscribe_CloseWindow_AndSetFlag()
        {
            var mockWindow = new Mock<IWindowService>();
            var mockFriends = new Mock<IFriendsManagerService>();
            var mockMusic = new Mock<BackgroundMusicService>();
            var vm = new SettingsViewModel(mockWindow.Object, mockFriends.Object, mockMusic.Object);

            var user = new PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO
            {
                UserAccountId =123,
                PlayerId =456
            };
            SessionManager.Login(user);

            vm.LogoutCommand.Execute(null);

            mockFriends.Verify(m => m.UnsubscribeFromFriendUpdatesAsync(user.UserAccountId), Times.Once);
            mockWindow.Verify(w => w.CloseWindow(vm), Times.Once);
            Assert.True(vm.WasLogoutSuccessful);
            Assert.False(SessionManager.IsUserLoggedIn());
        }

        [Fact]
        public void Logout_WhenGuestUser_ShouldNotUnsubscribeButCloseAndClearSession()
        {
            var mockWindow = new Mock<IWindowService>();
            var mockFriends = new Mock<IFriendsManagerService>();
            var mockMusic = new Mock<BackgroundMusicService>();
            var vm = new SettingsViewModel(mockWindow.Object, mockFriends.Object, mockMusic.Object);

            
            var user = new PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO
            {
                UserAccountId =999,
                PlayerId = -1
            };
            SessionManager.Login(user);

            vm.LogoutCommand.Execute(null);

            mockFriends.Verify(m => m.UnsubscribeFromFriendUpdatesAsync(It.IsAny<int>()), Times.Never);
            mockWindow.Verify(w => w.CloseWindow(vm), Times.Once);
            Assert.True(vm.WasLogoutSuccessful);
            Assert.False(SessionManager.IsUserLoggedIn());
        }
    }
}
