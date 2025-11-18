using BCrypt.Net;
using Data.DAL.Interfaces;
using Data.Model;
using Moq;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Services.Services;
using Services.Wrappers;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Test.ServicesTests
{
    public class LoginManagerTests
    {
        private readonly Mock<IAccountRepository> mockRepo;
        private readonly LoginManager loginManager;

        public LoginManagerTests()
        {
            mockRepo = new Mock<IAccountRepository>();
            loginManager = new LoginManager(mockRepo.Object);
        }
        
        [Fact]
        public async Task LoginAsync_ShouldReturnUserDTO_WhenCredentialsAreValidAndPlayerExists()
        {
            // Arrange
            var email = "test@example.com";
            var password = "ValidPassword123!";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            var mockPlayer = new Player { Id = 10 };
            var mockAccount = new UserAccount
            {
                Id = 1,
                Nickname = "testuser",
                Email = email,
                FirstName = "Test",
                LastName = "User",
                PasswordHash = hashedPassword,
                PhotoId = 1,
                Player = new List<Player> { mockPlayer },
                SocialAccount = new List<SocialAccount>()
            };

            mockRepo.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            // Act
            var result = await loginManager.LoginAsync(email, password);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(mockAccount.Id, result.UserAccountId);
            Assert.Equal(mockPlayer.Id, result.PlayerId);
            Assert.Equal(mockAccount.Nickname, result.Nickname);
            mockRepo.Verify(repo => repo.GetUserByEmailAsync(email), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnNull_WhenUserNotFound()
        {
            // Arrange
            var email = "nonexistent@example.com";
            var password = "password";

            mockRepo.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync((UserAccount?)null);

            // Act
            var result = await loginManager.LoginAsync(email, password);

            // Assert
            Assert.Null(result);
            mockRepo.Verify(repo => repo.GetUserByEmailAsync(email), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnNull_WhenPasswordIsInvalid()
        {
            // Arrange
            var email = "test@example.com";
            var correctPassword = "ValidPassword123!";
            var wrongPassword = "WrongPassword!";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(correctPassword);
            var mockAccount = new UserAccount
            {
                Id = 1,
                Nickname = "testuser",
                Email = email,
                PasswordHash = hashedPassword,
                Player = new List<Player> { new Player { Id = 10 } },
                SocialAccount = new List<SocialAccount>()
            };

            mockRepo.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            // Act
            var result = await loginManager.LoginAsync(email, wrongPassword);

            // Assert
            Assert.Null(result);
            mockRepo.Verify(repo => repo.GetUserByEmailAsync(email), Times.Once);
        }


        [Fact]
        public async Task LoginAsync_ShouldReturnNull_WhenUserHasNoPlayerAssociated()
        {
            // Arrange
            var email = "test@example.com";
            var password = "ValidPassword123!";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            var mockAccount = new UserAccount
            {
                Id = 1,
                Nickname = "testuser",
                Email = email,
                PasswordHash = hashedPassword,
                Player = new List<Player>(),
                SocialAccount = new List<SocialAccount>()
            };

            mockRepo.Setup(repo => repo.GetUserByEmailAsync(email)).ReturnsAsync(mockAccount);

            // Act
            var result = await loginManager.LoginAsync(email, password);

            // Assert
            Assert.Null(result);
            mockRepo.Verify(repo => repo.GetUserByEmailAsync(email), Times.Once);
        }

        public class MockDbException : DbException { 
            public MockDbException(string message) : base(message) { } 
        }

        [Fact]
        public async Task LoginAsync_ShouldThrowFaultException_WhenRepositoryThrowsDbException_Specific()
        {
            // Arrange
            var email = "db_error@example.com";
            var password = "password";

            mockRepo.Setup(repo => repo.GetUserByEmailAsync(email))
                    .ThrowsAsync(new MockDbException("Error de conexión simulado"));

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
    }
}
