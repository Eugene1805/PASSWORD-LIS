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
            var user = new PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO
            {
                UserAccountId = 123,
                PlayerId = 456,
                Nickname = "TestUser"
            };

            SessionManager.Login(user);

            Assert.NotNull(SessionManager.CurrentUser);
            Assert.Equal(123, SessionManager.CurrentUser.UserAccountId);
            Assert.Equal(456, SessionManager.CurrentUser.PlayerId);
            Assert.Equal("TestUser", SessionManager.CurrentUser.Nickname);

            SessionManager.Logout();
        }

        [Fact]
        public void Login_WithNullUser_ShouldSetCurrentUserToNull()
        {
            PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO user = null;

            SessionManager.Login(user);

            Assert.Null(SessionManager.CurrentUser);
        }

        [Fact]
        public void Login_CalledTwice_ShouldOverwritePreviousUser()
        {
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

            SessionManager.Login(firstUser);
            SessionManager.Login(secondUser);

            Assert.NotNull(SessionManager.CurrentUser);
            Assert.Equal(2, SessionManager.CurrentUser.UserAccountId);
            Assert.Equal(20, SessionManager.CurrentUser.PlayerId);
            Assert.Equal("SecondUser", SessionManager.CurrentUser.Nickname);

            SessionManager.Logout();
        }

        [Fact]
        public void Logout_WhenUserLoggedIn_ShouldClearCurrentUser()
        {
            var user = new PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO
            {
                UserAccountId = 123,
                PlayerId = 456
            };
            SessionManager.Login(user);

            SessionManager.Logout();

            Assert.Null(SessionManager.CurrentUser);
        }

        [Fact]
        public void Logout_WhenNoUserLoggedIn_ShouldDoNothing()
        {
            SessionManager.Logout();

            SessionManager.Logout();

            Assert.Null(SessionManager.CurrentUser);
        }

        [Fact]
        public void IsUserLoggedIn_WhenUserIsLoggedIn_ShouldReturnTrue()
        {
            var user = new PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO
            {
                UserAccountId = 123,
                PlayerId = 456
            };
            SessionManager.Login(user);

            var result = SessionManager.IsUserLoggedIn();

            Assert.True(result);

            SessionManager.Logout();
        }

        [Fact]
        public void IsUserLoggedIn_WhenNoUserIsLoggedIn_ShouldReturnFalse()
        {
            SessionManager.Logout();

            var result = SessionManager.IsUserLoggedIn();

            Assert.False(result);
        }

        [Fact]
        public void IsUserLoggedIn_AfterLogout_ShouldReturnFalse()
        {
            var user = new PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO
            {
                UserAccountId = 123,
                PlayerId = 456
            };
            SessionManager.Login(user);
            SessionManager.Logout();

            var result = SessionManager.IsUserLoggedIn();

            Assert.False(result);
        }

        [Fact]
        public void CurrentUser_BeforeLogin_ShouldBeNull()
        {
            SessionManager.Logout();

            Assert.Null(SessionManager.CurrentUser);
        }

        [Fact]
        public void Login_WithGuestUser_ShouldSetCurrentUser()
        {
            var guestUser = new PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO
            {
                UserAccountId = 999,
                PlayerId = -1,
                Nickname = "Guest"
            };

            SessionManager.Login(guestUser);

            Assert.NotNull(SessionManager.CurrentUser);
            Assert.Equal(999, SessionManager.CurrentUser.UserAccountId);
            Assert.Equal(-1, SessionManager.CurrentUser.PlayerId);

            SessionManager.Logout();
        }
    }
}
