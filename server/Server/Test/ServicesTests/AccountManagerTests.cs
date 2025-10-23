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
    }
}
