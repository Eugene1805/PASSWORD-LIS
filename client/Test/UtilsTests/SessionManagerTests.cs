using PASSWORD_LIS_Client.Utils;
using Xunit;

namespace Test.UtilsTests
{
    [Collection("SessionManager Tests")]
    public class SessionManagerTests
    {
        public SessionManagerTests()
        {
            SessionManager.Logout();
        }

        [Fact]
        public void Login_WithValidUser_ShouldSetCurrentUser()
        {
            // Arrange
            var user = new PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO
            {
                UserAccountId = 123,
                PlayerId = 456,
                Nickname = "TestUser"
            };

            // Act
            SessionManager.Login(user);

            // Assert
            Assert.NotNull(SessionManager.CurrentUser);
            Assert.Equal(123, SessionManager.CurrentUser.UserAccountId);
            Assert.Equal(456, SessionManager.CurrentUser.PlayerId);
            Assert.Equal("TestUser", SessionManager.CurrentUser.Nickname);

            // Cleanup
            SessionManager.Logout();
        }

        [Fact]
        public void Login_WithNullUser_ShouldSetCurrentUserToNull()
        {
            // Arrange
            PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO user = null;

            // Act
            SessionManager.Login(user);

            // Assert
            Assert.Null(SessionManager.CurrentUser);
        }

        [Fact]
        public void Login_CalledTwice_ShouldOverwritePreviousUser()
        {
            // Arrange
            var firstUser = new PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO
            {
                UserAccountId = 1,
                PlayerId = 10,
                Nickname = "FirstUser"
            };
            var secondUser = new PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO
            {
                UserAccountId = 2,
                PlayerId = 20,
                Nickname = "SecondUser"
            };

            // Act
            SessionManager.Login(firstUser);
            SessionManager.Login(secondUser);

            // Assert
            Assert.NotNull(SessionManager.CurrentUser);
            Assert.Equal(2, SessionManager.CurrentUser.UserAccountId);
            Assert.Equal(20, SessionManager.CurrentUser.PlayerId);
            Assert.Equal("SecondUser", SessionManager.CurrentUser.Nickname);

            // Cleanup
            SessionManager.Logout();
        }

        [Fact]
        public void Logout_WhenUserLoggedIn_ShouldClearCurrentUser()
        {
            // Arrange
            var user = new PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO
            {
                UserAccountId = 123,
                PlayerId = 456
            };
            SessionManager.Login(user);

            // Act
            SessionManager.Logout();

            // Assert
            Assert.Null(SessionManager.CurrentUser);
        }

        [Fact]
        public void Logout_WhenNoUserLoggedIn_ShouldDoNothing()
        {
            // Arrange
            SessionManager.Logout();

            // Act
            SessionManager.Logout();

            // Assert
            Assert.Null(SessionManager.CurrentUser);
        }

        [Fact]
        public void IsUserLoggedIn_WhenUserIsLoggedIn_ShouldReturnTrue()
        {
            // Arrange
            var user = new PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO
            {
                UserAccountId = 123,
                PlayerId = 456
            };
            SessionManager.Login(user);

            // Act
            var result = SessionManager.IsUserLoggedIn();

            // Assert
            Assert.True(result);

            // Cleanup
            SessionManager.Logout();
        }

        [Fact]
        public void IsUserLoggedIn_WhenNoUserIsLoggedIn_ShouldReturnFalse()
        {
            // Arrange
            SessionManager.Logout();

            // Act
            var result = SessionManager.IsUserLoggedIn();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsUserLoggedIn_AfterLogout_ShouldReturnFalse()
        {
            // Arrange
            var user = new PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO
            {
                UserAccountId = 123,
                PlayerId = 456
            };
            SessionManager.Login(user);
            SessionManager.Logout();

            // Act
            var result = SessionManager.IsUserLoggedIn();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CurrentUser_BeforeLogin_ShouldBeNull()
        {
            // Arrange
            SessionManager.Logout();

            // Act & Assert
            Assert.Null(SessionManager.CurrentUser);
        }

        [Fact]
        public void Login_WithGuestUser_ShouldSetCurrentUser()
        {
            // Arrange
            var guestUser = new PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO
            {
                UserAccountId = 999,
                PlayerId = -1,
                Nickname = "Guest"
            };

            // Act
            SessionManager.Login(guestUser);

            // Assert
            Assert.NotNull(SessionManager.CurrentUser);
            Assert.Equal(999, SessionManager.CurrentUser.UserAccountId);
            Assert.Equal(-1, SessionManager.CurrentUser.PlayerId);

            // Cleanup
            SessionManager.Logout();
        }
    }
}
