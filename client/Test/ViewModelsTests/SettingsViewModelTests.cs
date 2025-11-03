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
            // Arrange
            PASSWORD_LIS_Client.Properties.Settings.Default.languageCode = "en-US";
            var mockWindow = new Mock<IWindowService>();
            var mockFriends = new Mock<IFriendsManagerService>();
            var mockMusic = new Mock<BackgroundMusicService>();

            // Act
            var vm = new SettingsViewModel(mockWindow.Object, mockFriends.Object, mockMusic.Object);

            // Assert
            Assert.True(vm.IsEnglishSelected);
            Assert.False(vm.IsSpanishSelected);
        }

        [Fact]
        public void MusicVolume_Setter_ShouldUpdateViewModelValue()
        {
            // Arrange
            var mockWindow = new Mock<IWindowService>();
            var mockFriends = new Mock<IFriendsManagerService>();
            var mockMusic = new Mock<BackgroundMusicService>();
            double setValue =0.7;
            var vm = new SettingsViewModel(mockWindow.Object, mockFriends.Object, mockMusic.Object);

            // Act
            vm.MusicVolume = setValue;

            // Assert
            Assert.Equal(setValue, vm.MusicVolume,3);
        }

        [Fact]
        public void LanguageSelection_SetEnglish_ShouldUpdateSettingsAndProvider()
        {
            // Arrange
            var mockWindow = new Mock<IWindowService>();
            var mockFriends = new Mock<IFriendsManagerService>();
            var mockMusic = new Mock<BackgroundMusicService>();
            var vm = new SettingsViewModel(mockWindow.Object, mockFriends.Object, mockMusic.Object);

            // Act
            vm.IsEnglishSelected = true;

            // Assert
            Assert.Equal("en-US", PASSWORD_LIS_Client.Properties.Settings.Default.languageCode);
        }

        [Fact]
        public void LanguageSelection_SetSpanish_ShouldUpdateSettingsAndProvider()
        {
            // Arrange
            var mockWindow = new Mock<IWindowService>();
            var mockFriends = new Mock<IFriendsManagerService>();
            var mockMusic = new Mock<BackgroundMusicService>();
            var vm = new SettingsViewModel(mockWindow.Object, mockFriends.Object, mockMusic.Object);

            // Act
            vm.IsSpanishSelected = true;

            // Assert
            Assert.Equal("es-MX", PASSWORD_LIS_Client.Properties.Settings.Default.languageCode);
        }

        [Fact]
        public void Logout_WhenLoggedInRegisteredUser_ShouldUnsubscribe_CloseWindow_AndSetFlag()
        {
            // Arrange
            var mockWindow = new Mock<IWindowService>();
            var mockFriends = new Mock<IFriendsManagerService>();
            var mockMusic = new Mock<BackgroundMusicService>();
            var vm = new SettingsViewModel(mockWindow.Object, mockFriends.Object, mockMusic.Object);

            // Simulate logged in registered user
            var user = new PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO
            {
                UserAccountId =123,
                PlayerId =456
            };
            SessionManager.Login(user);

            // Act
            vm.LogoutCommand.Execute(null);

            // Assert
            mockFriends.Verify(m => m.UnsubscribeFromFriendUpdatesAsync(user.UserAccountId), Times.Once);
            mockWindow.Verify(w => w.CloseWindow(vm), Times.Once);
            Assert.True(vm.WasLogoutSuccessful);
            Assert.False(SessionManager.IsUserLoggedIn());
        }

        [Fact]
        public void Logout_WhenGuestUser_ShouldNotUnsubscribeButCloseAndClearSession()
        {
            // Arrange
            var mockWindow = new Mock<IWindowService>();
            var mockFriends = new Mock<IFriendsManagerService>();
            var mockMusic = new Mock<BackgroundMusicService>();
            var vm = new SettingsViewModel(mockWindow.Object, mockFriends.Object, mockMusic.Object);

            // Simulate logged in GUEST (PlayerId <0)
            var user = new PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO
            {
                UserAccountId =999,
                PlayerId = -1
            };
            SessionManager.Login(user);

            // Act
            vm.LogoutCommand.Execute(null);

            // Assert
            mockFriends.Verify(m => m.UnsubscribeFromFriendUpdatesAsync(It.IsAny<int>()), Times.Never);
            mockWindow.Verify(w => w.CloseWindow(vm), Times.Once);
            Assert.True(vm.WasLogoutSuccessful);
            Assert.False(SessionManager.IsUserLoggedIn());
        }
    }
}
