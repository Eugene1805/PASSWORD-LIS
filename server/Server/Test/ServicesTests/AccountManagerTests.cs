using Data.DAL.Interfaces;
using Data.Exceptions;
using Data.Model;
using Moq;
using System.ServiceModel;
using Services.Contracts.DTOs;
using Services.Services;
using Services.Util;

namespace Test.ServicesTests
{
    public class AccountManagerTests
    {
        private readonly Mock<IAccountRepository> mockRepo;
        private readonly Mock<INotificationService> mockNotification;
        private readonly Mock<IVerificationCodeService> mockCodeService;
        private readonly AccountManager accountManager;

        public AccountManagerTests()
        {
            mockRepo = new Mock<IAccountRepository>();
            mockNotification = new Mock<INotificationService>();
            mockCodeService = new Mock<IVerificationCodeService>();

            accountManager = new AccountManager(
                mockRepo.Object,
                mockNotification.Object,
                mockCodeService.Object
            );
        }

        [Fact]
        public async Task CreateAccount_ShouldCompleteSuccessfully_WhenDependenciesSucceed()
        {
            // Arrange
            var newAccountDto = new NewAccountDTO { Email = "test@example.com", Password = "Password123!" };
            var generatedCode = "123456";

            mockRepo.Setup(repo => repo.CreateAccountAsync(It.IsAny<UserAccount>()))
                     .Returns(Task.CompletedTask);

            mockCodeService.Setup(service => service.GenerateAndStoreCode(newAccountDto.Email, CodeType.EmailVerification))
                            .Returns(generatedCode);

            // Act
            await accountManager.CreateAccountAsync(newAccountDto);

            // Assert
            mockRepo.Verify(repo => repo.CreateAccountAsync(It.IsAny<UserAccount>()), Times.Once);
            mockCodeService.Verify(service => service.GenerateAndStoreCode(newAccountDto.Email, CodeType.EmailVerification), Times.Once);
            mockNotification.Verify(service => service.SendAccountVerificationEmailAsync(newAccountDto.Email, generatedCode), Times.Once);
        }

        [Fact]
        public async Task CreateAccount_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var newAccountDto = new NewAccountDTO
            {
                Email = "newuser@example.com",
                Password = "AValidPassword123!",
                Nickname = "Newbie",
                FirstName = "Test",
                LastName = "User"
            };
            var verificationCode = "123456";

            mockRepo.Setup(repo => repo.CreateAccountAsync(It.IsAny<UserAccount>()))
                     .Returns(Task.CompletedTask);

            mockCodeService.Setup(s => s.GenerateAndStoreCode(newAccountDto.Email, CodeType.EmailVerification))
                            .Returns(verificationCode);

            // Act
            await accountManager.CreateAccountAsync(newAccountDto);

            // Assert
            mockRepo.Verify(repo => repo.CreateAccountAsync(It.IsAny<UserAccount>()), Times.Once);

            mockCodeService.Verify(s => s.GenerateAndStoreCode(newAccountDto.Email, CodeType.EmailVerification), Times.Once);

            mockNotification.Verify(n => n.SendAccountVerificationEmailAsync(newAccountDto.Email, verificationCode), Times.Once);
        }


        [Fact]
        public async Task CreateAccount_ShouldThrowFaultException_WhenAccountAlreadyExists()
        {
            // Arrange
            var newAccountDto = new NewAccountDTO
            {
                Email = "duplicate@example.com",
                Password = "AValidPassword123!"
            };

            mockRepo.Setup(repo => repo.CreateAccountAsync(It.IsAny<UserAccount>()))
                     .ThrowsAsync(new DuplicateAccountException("User already exist"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => accountManager.CreateAccountAsync(newAccountDto)
            );

            Assert.Equal("USER_ALREADY_EXISTS", exception.Detail.ErrorCode);

            mockCodeService.Verify(service => service.GenerateAndStoreCode(It.IsAny<string>(), It.IsAny<CodeType>()), Times.Never);
            mockNotification.Verify(service => service.SendAccountVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CreateAccount_ShouldThrowFaultException_WhenDatabaseFails()
        {
            // Arrange
            var newAccountDto = new NewAccountDTO
            {
                Email = "test@example.com",
                Password = "AValidPassword123!"
            };

            mockRepo.Setup(repo => repo.CreateAccountAsync(It.IsAny<UserAccount>()))
                     .ThrowsAsync(new System.Data.Entity.Infrastructure.DbUpdateException("Error de BD"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => accountManager.CreateAccountAsync(newAccountDto)
            );

            Assert.Equal("DATABASE_ERROR", exception.Detail.ErrorCode);

            mockCodeService.Verify(service => service.GenerateAndStoreCode(It.IsAny<string>(), It.IsAny<CodeType>()), Times.Never);
            mockNotification.Verify(service => service.SendAccountVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        // New tests covering missing behaviors

        [Fact]
        public async Task CreateAccount_ShouldMapFields_AndHashPassword()
        {
            // Arrange
            var dto = new NewAccountDTO
            {
                Email = "map@test.com",
                Password = "Secret#123",
                Nickname = "Nick",
                FirstName = "First",
                LastName = "Last"
            };

            UserAccount? captured = null;
            mockRepo.Setup(r => r.CreateAccountAsync(It.IsAny<UserAccount>()))
                     .Callback<UserAccount>(ua => captured = ua)
                     .Returns(Task.CompletedTask);

            mockCodeService.Setup(c => c.GenerateAndStoreCode(dto.Email, CodeType.EmailVerification))
                            .Returns("999999");

            // Act
            await accountManager.CreateAccountAsync(dto);

            // Assert mapping and hashing
            Assert.NotNull(captured);
            Assert.Equal(dto.Email, captured!.Email);
            Assert.Equal(dto.Nickname, captured.Nickname);
            Assert.Equal(dto.FirstName, captured.FirstName);
            Assert.Equal(dto.LastName, captured.LastName);
            Assert.False(string.IsNullOrWhiteSpace(captured.PasswordHash));
            Assert.NotEqual(dto.Password, captured.PasswordHash); // hashed, not raw
        }

        [Fact]
        public async Task CreateAccount_ShouldReturnUnexpectedFault_WhenCodeGenerationFails()
        {
            // Arrange
            var dto = new NewAccountDTO { Email = "code@fail.com", Password = "P@ssw0rd!" };

            mockRepo.Setup(r => r.CreateAccountAsync(It.IsAny<UserAccount>()))
                     .Returns(Task.CompletedTask);

            mockCodeService.Setup(c => c.GenerateAndStoreCode(dto.Email, CodeType.EmailVerification))
                            .Throws(new Exception("code generation failed"));

            // Act
            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => accountManager.CreateAccountAsync(dto)
            );

            // Assert
            Assert.Equal("UNEXPECTED_ERROR", ex.Detail.ErrorCode);
            mockNotification.Verify(n => n.SendAccountVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CreateAccount_ShouldNotThrow_WhenNotificationServiceFails()
        {
            // Arrange
            var dto = new NewAccountDTO { Email = "notify@fail.com", Password = "P@ssw0rd!" };
            mockRepo.Setup(r => r.CreateAccountAsync(It.IsAny<UserAccount>()))
                     .Returns(Task.CompletedTask);
            mockCodeService.Setup(c => c.GenerateAndStoreCode(dto.Email, CodeType.EmailVerification))
                            .Returns("123456");

            // Return a faulted task without throwing synchronously
            mockNotification.Setup(n => n.SendAccountVerificationEmailAsync(dto.Email, It.IsAny<string>()))
                              .Returns(Task.FromException(new Exception("smtp error")));

            // Act - should complete without throwing because method does not await the notification task
            await accountManager.CreateAccountAsync(dto);

            // Assert: repository and code generation still invoked, notification attempted
            mockRepo.Verify(r => r.CreateAccountAsync(It.IsAny<UserAccount>()), Times.Once);
            mockCodeService.Verify(c => c.GenerateAndStoreCode(dto.Email, CodeType.EmailVerification), Times.Once);
            mockNotification.Verify(n => n.SendAccountVerificationEmailAsync(dto.Email, It.IsAny<string>()), Times.Once);
        }
    }
}
