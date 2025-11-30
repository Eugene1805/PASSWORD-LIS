using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using BCrypt.Net;
using Data.DAL.Interfaces;
using Data.Model;
using Moq;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Services.Services;
using Services.Util;
using Xunit;

namespace Test.ServicesTests
{
    public class LoginManagerTests
    {
        private readonly Mock<IAccountRepository> mockRepo;
        private readonly Mock<INotificationService> mockNotification;
        private readonly Mock<IVerificationCodeService> mockCodeService;
        private readonly LoginManager loginManager;

        public LoginManagerTests()
        {
            mockRepo = new Mock<IAccountRepository>();
            mockNotification = new Mock<INotificationService>();
            mockCodeService = new Mock<IVerificationCodeService>();
            loginManager = new LoginManager(mockRepo.Object, mockNotification.Object, mockCodeService.Object);
        }
        
        [Fact]
        public async Task LoginAsync_ShouldReturnUserDTO_WhenCredentialsAreValidAndPlayerExists()
        {
            // Arrange
            var email = "test@example.com";
            var password = "ValidPassword123!";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            var mockPlayer = new Player { Id = 10 };
            var mockAccount = new UserAccount { Id = 1, Nickname = "testuser", Email = email, FirstName = "Test", LastName = "User", PasswordHash = hashedPassword, PhotoId = 1, Player = new List<Player> { mockPlayer }, SocialAccount = new List<SocialAccount>() };

            mockRepo.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            // Act
            var result = await loginManager.LoginAsync(email, password);

            // Assert
            var expected = new { NotNull = true, UserAccountId = mockAccount.Id, PlayerId = mockPlayer.Id, Nickname = mockAccount.Nickname, Calls = 1 };
            var actual = new { NotNull = result != null, UserAccountId = result!.UserAccountId, PlayerId = result.PlayerId, Nickname = result.Nickname, Calls = mockRepo.Invocations.Count(i => i.Method.Name == nameof(IAccountRepository.GetUserByEmailAsync)) };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnInvalidDTO_WhenUserNotFound()
        {
            // Arrange
            var email = "nonexistent@example.com";
            var password = "password";

            mockRepo.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync((UserAccount?)null);

            // Act
            var result = await loginManager.LoginAsync(email, password);

            // Assert
            var expected = new { NotNull = true, UserAccountId = -1, Calls = 1 };
            var actual = new { NotNull = result != null, UserAccountId = result!.UserAccountId, Calls = mockRepo.Invocations.Count(i => i.Method.Name == nameof(IAccountRepository.GetUserByEmailAsync)) };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnInvalidDTO_WhenPasswordIsInvalid()
        {
            // Arrange
            var email = "test@example.com";
            var correctPassword = "ValidPassword123!";
            var wrongPassword = "WrongPassword!";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(correctPassword);
            var mockAccount = new UserAccount { Id = 1, Nickname = "testuser", Email = email, PasswordHash = hashedPassword, Player = new List<Player> { new Player { Id = 10 } }, SocialAccount = new List<SocialAccount>() };

            mockRepo.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            // Act
            var result = await loginManager.LoginAsync(email, wrongPassword);

            // Assert
            var expected = new { NotNull = true, UserAccountId = -1, Calls = 1 };
            var actual = new { NotNull = result != null, UserAccountId = result!.UserAccountId, Calls = mockRepo.Invocations.Count(i => i.Method.Name == nameof(IAccountRepository.GetUserByEmailAsync)) };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnInvalidDTO_WhenUserHasNoPlayerAssociated()
        {
            // Arrange
            var email = "test@example.com";
            var password = "ValidPassword123!";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            var mockAccount = new UserAccount { Id = 1, Nickname = "testuser", Email = email, PasswordHash = hashedPassword, Player = new List<Player>(), SocialAccount = new List<SocialAccount>() };

            mockRepo.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            // Act
            var result = await loginManager.LoginAsync(email, password);

            // Assert
            var expected = new { NotNull = true, UserAccountId = -1, Calls = 1 };
            var actual = new { NotNull = result != null, UserAccountId = result!.UserAccountId, Calls = mockRepo.Invocations.Count(i => i.Method.Name == nameof(IAccountRepository.GetUserByEmailAsync)) };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task LoginAsync_ShouldThrowFaultException_WhenRepositoryThrowsDbException_Specific()
        {
            // Arrange
            var email = "db_error@example.com";
            var password = "password";

            mockRepo.Setup(repo => repo.GetUserByEmailAsync(email))
                    .ThrowsAsync(new DbUpdateException("Error de conexión simulado", new Exception()));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => loginManager.LoginAsync(email, password)
            );

            // Assert
            Assert.Equal(ServiceErrorCode.DatabaseError, ex.Detail.Code);
            mockRepo.Verify(repo => repo.GetUserByEmailAsync(email), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_ShouldThrowFaultException_WhenBcryptFailsOrOtherGeneralException()
        {
            // Arrange
            var email = "test@example.com";
            var password = "ValidPassword123!";
            var invalidHash = "formato_invalido_de_hash";
            var mockAccount = new UserAccount
            {
                Id = 1,
                Email = email,
                PasswordHash = invalidHash,
                Player = new List<Player> { new Player { Id = 10 } },
                SocialAccount = new List<SocialAccount>()
            };

            mockRepo.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => loginManager.LoginAsync(email, password)
            );

            // Assert
            Assert.Equal(ServiceErrorCode.UnexpectedError, ex.Detail.Code);
            mockRepo.Verify(repo => repo.GetUserByEmailAsync(email), Times.Once);
        }

        [Fact]
        public async Task IsAccountVerifiedAsync_ShouldReturnTrue_WhenUserExistsAndIsVerified()
        {
            // Arrange
            var email = "verified@example.com";
            var mockAccount = new UserAccount
            {
                Id = 1,
                Email = email,
                EmailVerified = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
                Player = new List<Player>()
            };

            mockRepo.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            // Act
            var result = await loginManager.IsAccountVerifiedAsync(email);

            // Assert
            Assert.True(result);
            mockRepo.Verify(repo => repo.GetUserByEmailAsync(email), Times.Once);
        }

        [Fact]
        public async Task IsAccountVerifiedAsync_ShouldReturnFalse_WhenUserExistsAndIsNotVerified()
        {
            // Arrange
            var email = "unverified@example.com";
            var mockAccount = new UserAccount
            {
                Id = 1,
                Email = email,
                EmailVerified = false,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
                Player = new List<Player>()
            };

            mockRepo.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            // Act
            var result = await loginManager.IsAccountVerifiedAsync(email);

            // Assert
            Assert.False(result);
            mockRepo.Verify(repo => repo.GetUserByEmailAsync(email), Times.Once);
        }

        [Fact]
        public async Task IsAccountVerifiedAsync_ShouldReturnFalse_WhenUserNotFound()
        {
            // Arrange
            var email = "notfound@example.com";

            mockRepo.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync((UserAccount?)null);

            // Act
            var result = await loginManager.IsAccountVerifiedAsync(email);

            // Assert
            Assert.False(result);
            mockRepo.Verify(repo => repo.GetUserByEmailAsync(email), Times.Once);
        }

        [Fact]
        public async Task IsAccountVerifiedAsync_ShouldThrowFaultException_WhenRepositoryThrowsDbException()
        {
            // Arrange
            var email = "error@example.com";

            mockRepo.Setup(repo => repo.GetUserByEmailAsync(email))
                .ThrowsAsync(new DbUpdateException("Database error", new Exception()));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => loginManager.IsAccountVerifiedAsync(email)
            );

            Assert.Equal(ServiceErrorCode.DatabaseError, ex.Detail.Code);
            mockRepo.Verify(repo => repo.GetUserByEmailAsync(email), Times.Once);
        }

        [Fact]
        public async Task IsAccountVerifiedAsync_ShouldThrowFaultException_WhenUnexpectedErrorOccurs()
        {
            // Arrange
            var email = "error@example.com";

            mockRepo.Setup(repo => repo.GetUserByEmailAsync(email))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => loginManager.IsAccountVerifiedAsync(email)
            );

            Assert.Equal(ServiceErrorCode.UnexpectedError, ex.Detail.Code);
            mockRepo.Verify(repo => repo.GetUserByEmailAsync(email), Times.Once);
        }

        [Fact]
        public async Task SendVerificationCodeAsync_ShouldSend_WhenUserExistsAndNotVerified()
        {
            // Arrange
            var email = "code@ex.com";
            var account = new UserAccount { Email = email, EmailVerified = false, PasswordHash = BCrypt.Net.BCrypt.HashPassword("x"), Player = new List<Player>() };
            mockRepo.Setup(r => r.GetUserByEmailAsync(email)).ReturnsAsync(account);
            mockCodeService.Setup(c => c.GenerateAndStoreCode(email, CodeType.EmailVerification)).Returns("123456");

            // Act
            await loginManager.SendVerificationCodeAsync(email);

            // Assert
            mockCodeService.Verify(c => c.GenerateAndStoreCode(email, CodeType.EmailVerification), Times.Once);
            mockNotification.Verify(n => n.SendAccountVerificationEmailAsync(email, "123456"), Times.Once);
        }

        [Fact]
        public async Task SendVerificationCodeAsync_ShouldNotSend_WhenUserVerified()
        {
            // Arrange
            var email = "verified@ex.com";
            var account = new UserAccount { Email = email, EmailVerified = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword("x"), Player = new List<Player>() };
            mockRepo.Setup(r => r.GetUserByEmailAsync(email)).ReturnsAsync(account);

            // Act
            await loginManager.SendVerificationCodeAsync(email);

            // Assert
            mockCodeService.Verify(c => c.GenerateAndStoreCode(It.IsAny<string>(), It.IsAny<CodeType>()), Times.Never);
            mockNotification.Verify(n => n.SendAccountVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SendVerificationCodeAsync_ShouldNotSend_WhenUserNotFound()
        {
            // Arrange
            var email = "missing@ex.com";
            mockRepo.Setup(r => r.GetUserByEmailAsync(email)).ReturnsAsync((UserAccount?)null);

            // Act
            await loginManager.SendVerificationCodeAsync(email);

            // Assert
            mockCodeService.Verify(c => c.GenerateAndStoreCode(It.IsAny<string>(), It.IsAny<CodeType>()), Times.Never);
            mockNotification.Verify(n => n.SendAccountVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SendVerificationCodeAsync_ShouldThrowFault_WhenRepositoryThrows()
        {
            // Arrange
            var email = "error@ex.com";
            mockRepo.Setup(r => r.GetUserByEmailAsync(email)).ThrowsAsync(new Exception("db fail"));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(() => loginManager.SendVerificationCodeAsync(email));
            Assert.Equal(ServiceErrorCode.UnexpectedError, ex.Detail.Code);
        }

        [Fact]
        public async Task SendVerificationCodeAsync_ShouldThrowFault_WhenDbExceptionOccurs()
        {
            // Arrange
            var email = "dberror@ex.com";
            mockRepo.Setup(r => r.GetUserByEmailAsync(email)).ThrowsAsync(new DbUpdateException("Database error", new Exception()));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(() => loginManager.SendVerificationCodeAsync(email));
            Assert.Equal(ServiceErrorCode.DatabaseError, ex.Detail.Code);
        }

        [Fact]
        public async Task LoginAsync_ShouldMapAllUserProperties_WhenLoginSuccessful()
        {
            // Arrange
            var email = "fulltest@example.com";
            var password = "ValidPassword123!";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            var mockPlayer = new Player { Id = 10 };
            var socialAccount1 = new SocialAccount { Provider = "Google", Username = "user@google.com" };
            var socialAccount2 = new SocialAccount { Provider = "Facebook", Username = "user@facebook.com" };
            var mockAccount = new UserAccount { Id = 1, Nickname = "testuser", Email = email, FirstName = "Test", LastName = "User", PasswordHash = hashedPassword, PhotoId = 5, Player = new List<Player> { mockPlayer }, SocialAccount = new List<SocialAccount> { socialAccount1, socialAccount2 } };

            mockRepo.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            // Act
            var result = await loginManager.LoginAsync(email, password);

            // Assert
            var expected = new { NotNull = true, UserAccountId = 1, PlayerId = 10, Nickname = "testuser", Email = email, FirstName = "Test", LastName = "User", PhotoId = 5, SocialCount = 2, Google = "user@google.com", Facebook = "user@facebook.com" };
            var actual = new { NotNull = result != null, UserAccountId = result!.UserAccountId, PlayerId = result.PlayerId, Nickname = result.Nickname, Email = result.Email, FirstName = result.FirstName, LastName = result.LastName, PhotoId = result.PhotoId, SocialCount = result.SocialAccounts.Count, Google = result.SocialAccounts["Google"], Facebook = result.SocialAccounts["Facebook"] };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task LoginAsync_ShouldMapPhotoIdAsZero_WhenPhotoIdIsNull()
        {
            // Arrange
            var email = "nophoto@example.com";
            var password = "ValidPassword123!";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            var mockPlayer = new Player { Id = 10 };
            var mockAccount = new UserAccount
            {
                Id = 1,
                Nickname = "testuser",
                Email = email,
                PasswordHash = hashedPassword,
                PhotoId = null,
                Player = new List<Player> { mockPlayer },
                SocialAccount = new List<SocialAccount>()
            };

            mockRepo.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            // Act
            var result = await loginManager.LoginAsync(email, password);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.PhotoId);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnInvalidDTO_WhenPlayerIsNull()
        {
            // Arrange
            var email = "nullplayer@example.com";
            var password = "ValidPassword123!";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            var mockAccount = new UserAccount
            {
                Id = 1,
                Nickname = "testuser",
                Email = email,
                PasswordHash = hashedPassword,
                Player = new List<Player?> { null },
                SocialAccount = new List<SocialAccount>()
            };

            mockRepo.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            // Act
            var result = await loginManager.LoginAsync(email, password);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(-1, result.UserAccountId);
        }
    }
}
