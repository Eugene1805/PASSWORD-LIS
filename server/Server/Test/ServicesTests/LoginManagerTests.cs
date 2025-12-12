using System.Data.Entity.Infrastructure;
using System.ServiceModel;
using Data.DAL.Interfaces;
using Data.Model;
using Moq;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Services.Services;
using Services.Util;

namespace Test.ServicesTests
{
    public class LoginManagerTests
    {
        private readonly Mock<IAccountRepository> mockRepository;
        private readonly Mock<INotificationService> mockNotification;
        private readonly Mock<IVerificationCodeService> mockCodeService;
        private readonly LoginManager loginManager;

        public LoginManagerTests()
        {
            mockRepository = new Mock<IAccountRepository>();
            mockNotification = new Mock<INotificationService>();
            mockCodeService = new Mock<IVerificationCodeService>();
            loginManager = new LoginManager(mockRepository.Object, mockNotification.Object, mockCodeService.Object);
        }
        
        [Fact]
        public async Task LoginAsync_ShouldReturnUserDTO_WhenCredentialsAreValidAndPlayerExists()
        {
            var email = "test@example.com";
            var password = "ValidPassword123!";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            var mockPlayer = new Player 
            { 
                Id = 10 
            };
            var mockAccount = new UserAccount 
            { 
                Id = 1, Nickname = "testuser", Email = email, 
                FirstName = "Test", LastName = "User", PasswordHash = hashedPassword, 
                PhotoId = 1, Player = new List<Player> 
                { 
                    mockPlayer 
                }, SocialAccount = new List<SocialAccount>() 
            };

            mockRepository.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            var result = await loginManager.LoginAsync(email, password);

            var expected = new 
            {
                NotNull = true, UserAccountId = mockAccount.Id, PlayerId = mockPlayer.Id, 
                Nickname = mockAccount.Nickname, Calls = 1 
            };
            var actual = new 
            { 
                NotNull = result != null, UserAccountId = result!.UserAccountId,
                PlayerId = result.PlayerId, Nickname = result.Nickname, 
                Calls = mockRepository.Invocations.Count(i => 
                i.Method.Name == nameof(IAccountRepository.GetUserByEmailAsync)) 
            };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnInvalidDTO_WhenUserNotFound()
        {
            var email = "nonexistent@example.com";
            var password = "password";

            mockRepository.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync((UserAccount?)null);

            var result = await loginManager.LoginAsync(email, password);

            var expected = new 
            { 
                NotNull = true, UserAccountId = -1, Calls = 1 
            };
            var actual = new 
            { 
                NotNull = result != null, UserAccountId = result!.UserAccountId,
                Calls = mockRepository.Invocations.Count(i => 
                i.Method.Name == nameof(IAccountRepository.GetUserByEmailAsync)) 
            };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnInvalidDTO_WhenPasswordIsInvalid()
        {
            var email = "test@example.com";
            var correctPassword = "ValidPassword123!";
            var wrongPassword = "WrongPassword!";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(correctPassword);
            var mockAccount = new UserAccount 
            { 
                Id = 1, Nickname = "testuser", 
                Email = email, PasswordHash = hashedPassword, Player = new List<Player> 
                { 
                    new Player { Id = 10 
                    } 
                },
                SocialAccount = new List<SocialAccount>() 
            };

            mockRepository.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            var result = await loginManager.LoginAsync(email, wrongPassword);

            var expected = new 
            { 
                NotNull = true, UserAccountId = -1, Calls = 1 
            };
            var actual = new 
            { 
                NotNull = result != null, UserAccountId = result!.UserAccountId,
                Calls = mockRepository.Invocations.Count(i => 
                i.Method.Name == nameof(IAccountRepository.GetUserByEmailAsync)) 
            };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnInvalidDTO_WhenUserHasNoPlayerAssociated()
        {
            var email = "test@example.com";
            var password = "ValidPassword123!";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            var mockAccount = new UserAccount 
            { 
                Id = 1, Nickname = "testuser", Email = email,
                PasswordHash = hashedPassword, Player = new List<Player>(), SocialAccount = new List<SocialAccount>()
            };

            mockRepository.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            var result = await loginManager.LoginAsync(email, password);

            var expected = new 
            { 
                NotNull = true, UserAccountId = -1, Calls = 1 
            };
            var actual = new 
            { 
                NotNull = result != null, UserAccountId = result!.UserAccountId,
                Calls = mockRepository.Invocations.Count(i => 
                i.Method.Name == nameof(IAccountRepository.GetUserByEmailAsync)) 
            };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task LoginAsync_ShouldThrowFaultException_WhenRepositoryThrowsDbException_Specific()
        {
            var email = "db_error@example.com";
            var password = "password";

            mockRepository.Setup(repo => repo.GetUserByEmailAsync(email))
                    .ThrowsAsync(new DbUpdateException("Error de conexión simulado", new Exception()));

            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => loginManager.LoginAsync(email, password)
            );

            Assert.Equal(ServiceErrorCode.DatabaseError, ex.Detail.Code);
            mockRepository.Verify(repo => repo.GetUserByEmailAsync(email), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_ShouldThrowFaultException_WhenBcryptFailsOrOtherGeneralException()
        {
            var email = "test@example.com";
            var password = "ValidPassword123!";
            var invalidHash = "formato_invalido_de_hash";
            var mockAccount = new UserAccount
            {
                Id = 1,
                Email = email,
                PasswordHash = invalidHash,
                Player = new List<Player> 
                {
                    new Player 
                    { 
                        Id = 10 
                    } 
                },
                SocialAccount = new List<SocialAccount>()
            };

            mockRepository.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => loginManager.LoginAsync(email, password)
            );

            Assert.Equal(ServiceErrorCode.UnexpectedError, ex.Detail.Code);
            mockRepository.Verify(repo => repo.GetUserByEmailAsync(email), Times.Once);
        }

        [Fact]
        public async Task IsAccountVerifiedAsync_ShouldReturnTrue_WhenUserExistsAndIsVerified()
        {
            var email = "verified@example.com";
            var mockAccount = new UserAccount
            {
                Id = 1,
                Email = email,
                EmailVerified = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
                Player = new List<Player>()
            };

            mockRepository.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            var result = await loginManager.IsAccountVerifiedAsync(email);

            Assert.True(result);
            mockRepository.Verify(repo => repo.GetUserByEmailAsync(email), Times.Once);
        }

        [Fact]
        public async Task IsAccountVerifiedAsync_ShouldReturnFalse_WhenUserExistsAndIsNotVerified()
        {
            var email = "unverified@example.com";
            var mockAccount = new UserAccount
            {
                Id = 1,
                Email = email,
                EmailVerified = false,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
                Player = new List<Player>()
            };

            mockRepository.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            var result = await loginManager.IsAccountVerifiedAsync(email);

            Assert.False(result);
            mockRepository.Verify(repo => repo.GetUserByEmailAsync(email), Times.Once);
        }

        [Fact]
        public async Task IsAccountVerifiedAsync_ShouldReturnFalse_WhenUserNotFound()
        {
            var email = "notfound@example.com";

            mockRepository.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync((UserAccount?)null);

            var result = await loginManager.IsAccountVerifiedAsync(email);

            Assert.False(result);
            mockRepository.Verify(repo => repo.GetUserByEmailAsync(email), Times.Once);
        }

        [Fact]
        public async Task IsAccountVerifiedAsync_ShouldThrowFaultException_WhenRepositoryThrowsDbException()
        {
            var email = "error@example.com";

            mockRepository.Setup(repo => repo.GetUserByEmailAsync(email))
                .ThrowsAsync(new DbUpdateException("Database error", new Exception()));

            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => loginManager.IsAccountVerifiedAsync(email)
            );

            Assert.Equal(ServiceErrorCode.DatabaseError, ex.Detail.Code);
            mockRepository.Verify(repo => repo.GetUserByEmailAsync(email), Times.Once);
        }

        [Fact]
        public async Task IsAccountVerifiedAsync_ShouldThrowFaultException_WhenUnexpectedErrorOccurs()
        {
            var email = "error@example.com";

            mockRepository.Setup(repo => repo.GetUserByEmailAsync(email))
                .ThrowsAsync(new Exception("Unexpected error"));

            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => loginManager.IsAccountVerifiedAsync(email)
            );

            Assert.Equal(ServiceErrorCode.UnexpectedError, ex.Detail.Code);
            mockRepository.Verify(repo => repo.GetUserByEmailAsync(email), Times.Once);
        }

        [Fact]
        public async Task SendVerificationCodeAsync_ShouldSend_WhenUserExistsAndNotVerified()
        {
            var email = "code@ex.com";
            var account = new UserAccount 
            { 
                Email = email, EmailVerified = false, 
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("x"), Player = new List<Player>() 
            };
            mockRepository.Setup(r => r.GetUserByEmailAsync(email)).ReturnsAsync(account);
            mockCodeService.Setup(c => c.GenerateAndStoreCode(email, CodeType.EmailVerification)).Returns("123456");

            await loginManager.SendVerificationCodeAsync(email);

            mockCodeService.Verify(c => c.GenerateAndStoreCode(email, CodeType.EmailVerification), Times.Once);
            mockNotification.Verify(n => n.SendAccountVerificationEmailAsync(email, "123456"), Times.Once);
        }

        [Fact]
        public async Task SendVerificationCodeAsync_ShouldNotSend_WhenUserVerified()
        {
            var email = "verified@ex.com";
            var account = new UserAccount 
            { 
                Email = email, EmailVerified = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("x"), Player = new List<Player>() 
            };
            mockRepository.Setup(r => r.GetUserByEmailAsync(email)).ReturnsAsync(account);

            await loginManager.SendVerificationCodeAsync(email);

            mockCodeService.Verify(c => c.GenerateAndStoreCode(It.IsAny<string>(), It.IsAny<CodeType>()), Times.Never);
            mockNotification.Verify(n => n.SendAccountVerificationEmailAsync(It.IsAny<string>(),
                It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SendVerificationCodeAsync_ShouldNotSend_WhenUserNotFound()
        {
            var email = "missing@ex.com";
            mockRepository.Setup(r => r.GetUserByEmailAsync(email)).ReturnsAsync((UserAccount?)null);

            await loginManager.SendVerificationCodeAsync(email);

            mockCodeService.Verify(c => c.GenerateAndStoreCode(It.IsAny<string>(), It.IsAny<CodeType>()), Times.Never);
            mockNotification.Verify(n => n.SendAccountVerificationEmailAsync(It.IsAny<string>(), 
                It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SendVerificationCodeAsync_ShouldThrowFault_WhenRepositoryThrows()
        {
            var email = "error@ex.com";
            mockRepository.Setup(r => r.GetUserByEmailAsync(email)).ThrowsAsync(new Exception("db fail"));

            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(() => 
            loginManager.SendVerificationCodeAsync(email));
            Assert.Equal(ServiceErrorCode.UnexpectedError, ex.Detail.Code);
        }

        [Fact]
        public async Task SendVerificationCodeAsync_ShouldThrowFault_WhenDbExceptionOccurs()
        {
            var email = "dberror@ex.com";
            mockRepository.Setup(r => r.GetUserByEmailAsync(email)).ThrowsAsync(new DbUpdateException("Database error",
                new Exception()));

            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(()
                => loginManager.SendVerificationCodeAsync(email));
            Assert.Equal(ServiceErrorCode.DatabaseError, ex.Detail.Code);
        }

        [Fact]
        public async Task LoginAsync_ShouldMapAllUserProperties_WhenLoginSuccessful()
        {
            var email = "fulltest@example.com";
            var password = "ValidPassword123!";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            var mockPlayer = new Player 
            { 
                Id = 10 
            };
            var socialAccount1 = new SocialAccount 
            { 
                Provider = "Instagram", Username = "user@instagram.com" 
            };
            var socialAccount2 = new SocialAccount 
            { 
                Provider = "Facebook", Username = "user@facebook.com" 
            };
            var mockAccount = new UserAccount 
            { 
                Id = 1, Nickname = "testuser", Email = email, FirstName = "Test",
                LastName = "User", PasswordHash = hashedPassword, PhotoId = 5, Player = new List<Player> 
                { 
                    mockPlayer 
                }, 
                SocialAccount = new List<SocialAccount> 
                { 
                    socialAccount1, socialAccount2 
                } 
            };

            mockRepository.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            var result = await loginManager.LoginAsync(email, password);

            var expected = new 
            { 
                NotNull = true, UserAccountId = 1, PlayerId = 10, Nickname = "testuser", 
                Email = email, FirstName = "Test", LastName = "User", PhotoId = 5, SocialCount = 2, 
                Instagram = "user@instagram.com", Facebook = "user@facebook.com" 
            };
            var actual = new 
            { 
                NotNull = result != null, UserAccountId = result!.UserAccountId,
                PlayerId = result.PlayerId, Nickname = result.Nickname, Email = result.Email, 
                FirstName = result.FirstName, LastName = result.LastName, PhotoId = result.PhotoId,
                SocialCount = result.SocialAccounts.Count, Instagram = result.SocialAccounts["Instagram"],
                Facebook = result.SocialAccounts["Facebook"] 
            };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task LoginAsync_ShouldMapPhotoIdAsZero_WhenPhotoIdIsNull()
        {
            var email = "nophoto@example.com";
            var password = "ValidPassword123!";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            var mockPlayer = new Player 
            { 
                Id = 10 
            };
            var mockAccount = new UserAccount
            {
                Id = 1,
                Nickname = "testuser",
                Email = email,
                PasswordHash = hashedPassword,
                PhotoId = null,
                Player = new List<Player> 
                { 
                    mockPlayer 
                },
                SocialAccount = new List<SocialAccount>()
            };

            mockRepository.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            var result = await loginManager.LoginAsync(email, password);

            Assert.NotNull(result);
            Assert.Equal(0, result.PhotoId);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnInvalidDTO_WhenPlayerIsNull()
        {
            var email = "nullplayer@example.com";
            var password = "ValidPassword123!";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            var mockAccount = new UserAccount
            {
                Id = 1,
                Nickname = "testuser",
                Email = email,
                PasswordHash = hashedPassword,
                Player = new List<Player?> 
                { 
                    null 
                },
                SocialAccount = new List<SocialAccount>()
            };

            mockRepository.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            var result = await loginManager.LoginAsync(email, password);

            Assert.NotNull(result);
            Assert.Equal(-1, result.UserAccountId);
        }
    }
}
