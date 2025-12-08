using Data.DAL.Interfaces;
using Data.Exceptions;
using Data.Model;
using Moq;
using Services.Contracts.DTOs;
using Services.Services;
using Services.Util;
using System.Data.Entity.Core;
using System.ServiceModel;

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
            NewAccountDTO newAccountDto = new NewAccountDTO { Email = "test@example.com", Password = "Password123!" };
            string generatedCode = "123456";

            mockRepo.Setup(repo => repo.CreateAccountAsync(It.IsAny<UserAccount>()))
                     .Returns(Task.CompletedTask);

            mockCodeService.Setup(service => service.GenerateAndStoreCode(newAccountDto.Email,
                CodeType.EmailVerification)).Returns(generatedCode);

            // Act
            await accountManager.CreateAccountAsync(newAccountDto);

            int repoCalls = mockRepo.Invocations.Count(i => 
            i.Method.Name == nameof(IAccountRepository.CreateAccountAsync));
            int codeCalls = mockCodeService.Invocations.Count(i => 
            i.Method.Name == nameof(IVerificationCodeService.GenerateAndStoreCode));
            int notificationCalls = mockNotification.Invocations.Count(i => 
            i.Method.Name == nameof(INotificationService.SendAccountVerificationEmailAsync));
            string? repoEmailArg = (mockRepo.Invocations.First(i => 
            i.Method.Name == nameof(IAccountRepository.CreateAccountAsync)).Arguments[0] as UserAccount)?.Email;

            string? codeEmailArg = mockCodeService.Invocations[0].Arguments[0] as string;
            CodeType codeTypeArg = (CodeType)mockCodeService.Invocations[0].Arguments[1];
            string? notifEmailArg = mockNotification.Invocations[0].Arguments[0] as string;
            string? notifCodeArg = mockNotification.Invocations[0].Arguments[1] as string;

            var expected = new
            {
                RepoCalls = 1,
                CodeCalls = 1,
                NotificationCalls = 1,
                Email = (string?)newAccountDto.Email,
                CodeType = CodeType.EmailVerification,
                GeneratedCode = (string?)generatedCode
            };

            var actual = new
            {
                RepoCalls = repoCalls,
                CodeCalls = codeCalls,
                NotificationCalls = notificationCalls,
                Email = repoEmailArg == codeEmailArg && codeEmailArg == notifEmailArg ? repoEmailArg : null,
                CodeType = codeTypeArg,
                GeneratedCode = notifCodeArg
            };

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task CreateAccount_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            NewAccountDTO newAccountDto = new NewAccountDTO
            {
                Email = "newuser@example.com",
                Password = "AValidPassword123!",
                Nickname = "Newbie",
                FirstName = "Test",
                LastName = "User"
            };
            string verificationCode = "123456";

            mockRepo.Setup(repo => repo.CreateAccountAsync(It.IsAny<UserAccount>()))
                     .Returns(Task.CompletedTask);

            mockCodeService.Setup(s => s.GenerateAndStoreCode(newAccountDto.Email, CodeType.EmailVerification))
                            .Returns(verificationCode);

            // Act
            await accountManager.CreateAccountAsync(newAccountDto);

            UserAccount? createdAccount = mockRepo.Invocations
                .Where(i => i.Method.Name == nameof(IAccountRepository.CreateAccountAsync))
                .Select(i => i.Arguments[0] as UserAccount)
                .FirstOrDefault();
            string? notifCode = mockNotification.Invocations.First(i => 
            i.Method.Name == nameof(INotificationService.SendAccountVerificationEmailAsync)).Arguments[1] as string;

            var expected = new
            {
                RepoCalls = 1,
                CodeCalls = 1,
                NotificationCalls = 1,
                Email = (string?)newAccountDto.Email,
                Nickname = (string?)newAccountDto.Nickname,
                FirstName = (string?)newAccountDto.FirstName,
                LastName = (string?)newAccountDto.LastName,
                VerificationCode = (string?)verificationCode
            };

            var actual = new
            {
                RepoCalls = mockRepo.Invocations.Count(i => 
                i.Method.Name == nameof(IAccountRepository.CreateAccountAsync)),
                CodeCalls = mockCodeService.Invocations.Count(i => 
                i.Method.Name == nameof(IVerificationCodeService.GenerateAndStoreCode)),
                NotificationCalls = mockNotification.Invocations.Count(i => 
                i.Method.Name == nameof(INotificationService.SendAccountVerificationEmailAsync)),
                Email = createdAccount?.Email,
                Nickname = createdAccount?.Nickname,
                FirstName = createdAccount?.FirstName,
                LastName = createdAccount?.LastName,
                VerificationCode = notifCode
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task CreateAccount_ShouldThrowFaultException_WhenAccountAlreadyExists()
        {
            // Arrange
            NewAccountDTO newAccountDto = new NewAccountDTO
            {
                Email = "duplicate@example.com",
                Password = "AValidPassword123!"
            };

            mockRepo.Setup(repo => repo.CreateAccountAsync(It.IsAny<UserAccount>()))
                     .ThrowsAsync(new DuplicateAccountException("User already exist"));

            // Act
            FaultException<ServiceErrorDetailDTO> exception = await
                Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => accountManager.CreateAccountAsync(newAccountDto)
            );

            var expected = new
            {
                ErrorCode = "USER_ALREADY_EXISTS",
                RepoCalls = 1, 
                CodeCalls = 0,
                NotificationCalls = 0
            };

            var actual = new
            {
                ErrorCode = exception.Detail.ErrorCode,
                RepoCalls = mockRepo.Invocations.Count(i => 
                i.Method.Name == nameof(IAccountRepository.CreateAccountAsync)),
                CodeCalls = mockCodeService.Invocations.Count(i => 
                i.Method.Name == nameof(IVerificationCodeService.GenerateAndStoreCode)),
                NotificationCalls = mockNotification.Invocations.Count(i => 
                i.Method.Name == nameof(INotificationService.SendAccountVerificationEmailAsync))
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task CreateAccount_ShouldThrowFaultException_WhenDatabaseFails()
        {
            // Arrange
            NewAccountDTO newAccountDto = new NewAccountDTO
            {
                Email = "test@example.com",
                Password = "AValidPassword123!"
            };

            mockRepo.Setup(repo => repo.CreateAccountAsync(It.IsAny<UserAccount>()))
                     .ThrowsAsync(new System.Data.Entity.Infrastructure.DbUpdateException("Error de BD"));

            // Act
            FaultException<ServiceErrorDetailDTO> exception = await 
                Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => accountManager.CreateAccountAsync(newAccountDto)
            );

            var expected = new
            {
                ErrorCode = "DATABASE_ERROR",
                RepoCalls = 1,
                CodeCalls = 0,
                NotificationCalls = 0
            };

            var actual = new
            {
                ErrorCode = exception.Detail.ErrorCode,
                RepoCalls = mockRepo.Invocations.Count(i => 
                i.Method.Name == nameof(IAccountRepository.CreateAccountAsync)),
                CodeCalls = mockCodeService.Invocations.Count(i => 
                i.Method.Name == nameof(IVerificationCodeService.GenerateAndStoreCode)),
                NotificationCalls = mockNotification.Invocations.Count(i => 
                i.Method.Name == nameof(INotificationService.SendAccountVerificationEmailAsync))
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task CreateAccount_ShouldMapFields_AndHashPassword()
        {
            // Arrange
            NewAccountDTO dto = new NewAccountDTO
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

            var expected = new
            {
                Email = (string?)dto.Email,
                Nickname = (string?)dto.Nickname,
                FirstName = (string?)dto.FirstName,
                LastName = (string?)dto.LastName,
                PasswordIsHashed = true,
                PasswordNotRaw = true,
                RepoCalls = 1
            };

            var actual = new
            {
                Email = captured?.Email,
                Nickname = captured?.Nickname,
                FirstName = captured?.FirstName,
                LastName = captured?.LastName,
                PasswordIsHashed = !string.IsNullOrWhiteSpace(captured?.PasswordHash),
                PasswordNotRaw = captured?.PasswordHash != dto.Password,
                RepoCalls = mockRepo.Invocations.Count(i => 
                i.Method.Name == nameof(IAccountRepository.CreateAccountAsync))
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task CreateAccount_ShouldReturnUnexpectedFault_WhenCodeGenerationFails()
        {
            // Arrange
            NewAccountDTO dto = new NewAccountDTO { Email = "code@fail.com", Password = "P@ssw0rd!" };

            mockRepo.Setup(r => r.CreateAccountAsync(It.IsAny<UserAccount>()))
                     .Returns(Task.CompletedTask);

            mockCodeService.Setup(c => c.GenerateAndStoreCode(dto.Email, CodeType.EmailVerification))
                            .Throws(new Exception("code generation failed"));

            // Act
            FaultException<ServiceErrorDetailDTO> ex = await 
                Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => accountManager.CreateAccountAsync(dto)
            );

            var expected = new
            {
                ErrorCode = "UNEXPECTED_ERROR",
                RepoCalls = 1,
                CodeCalls = 1,
                NotificationCalls = 0
            };

            var actual = new
            {
                ErrorCode = ex.Detail.ErrorCode,
                RepoCalls = mockRepo.Invocations.Count(i => 
                i.Method.Name == nameof(IAccountRepository.CreateAccountAsync)),
                CodeCalls = mockCodeService.Invocations.Count(i => 
                i.Method.Name == nameof(IVerificationCodeService.GenerateAndStoreCode)),
                NotificationCalls = mockNotification.Invocations.Count(i => 
                i.Method.Name == nameof(INotificationService.SendAccountVerificationEmailAsync))
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task CreateAccount_ShouldThrow_WhenNotificationServiceFails()
        {
            // Arrange
            NewAccountDTO dto = new NewAccountDTO { Email = "notify@fail.com", Password = "P@ssw0rd!" };
            mockRepo.Setup(r => r.CreateAccountAsync(It.IsAny<UserAccount>()))
                     .Returns(Task.CompletedTask);
            mockCodeService.Setup(c => c.GenerateAndStoreCode(dto.Email, CodeType.EmailVerification))
                            .Returns("123456");

            mockNotification.Setup(n => n.SendAccountVerificationEmailAsync(dto.Email, It.IsAny<string>()))
                              .ThrowsAsync(new System.Net.Mail.SmtpException("SMTP server error"));

            // Act
            FaultException<ServiceErrorDetailDTO> exception = await 
                Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => accountManager.CreateAccountAsync(dto)
            );

            var expected = new
            {
                ErrorCode = "EMAIL_SENDING_ERROR",
                RepoCalls = 1,
                CodeCalls = 1,
                NotificationCalls = 1
            };

            var actual = new
            {
                ErrorCode = exception.Detail.ErrorCode,
                RepoCalls = mockRepo.Invocations.Count(i => 
                i.Method.Name == nameof(IAccountRepository.CreateAccountAsync)),
                CodeCalls = mockCodeService.Invocations.Count(i => 
                i.Method.Name == nameof(IVerificationCodeService.GenerateAndStoreCode)),
                NotificationCalls = mockNotification.Invocations.Count(i => 
                i.Method.Name == nameof(INotificationService.SendAccountVerificationEmailAsync))
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task IsNicknameInUse_ShouldReturnRepositoryResult_WhenNicknameIsNotNull()
        {
            // Arrange
            string nickname = "ExistingUser";

            mockRepo.Setup(repo => repo.IsNicknameInUse(nickname))
                    .ReturnsAsync(true);

            // Act
            bool result = await accountManager.IsNicknameInUse(nickname);

            var expected = new
            {
                Result = true,
                RepoCalls = 1
            };

            var actual = new
            {
                Result = result,
                RepoCalls = mockRepo.Invocations.Count(i => i.Method.Name == nameof(IAccountRepository.IsNicknameInUse))
            };

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task IsNicknameInUse_ShouldReturnFalse_WhenNicknameIsNull()
        {
            // Arrange
            string? nickname = null;


            // Act
            bool result = await accountManager.IsNicknameInUse(nickname);

            var expected = new
            {
                Result = false,
                RepoCalls = 0
            };

            var actual = new
            {
                Result = result,
                RepoCalls = mockRepo.Invocations.Count(i => i.Method.Name == nameof(IAccountRepository.IsNicknameInUse))
            };

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task IsNicknameInUse_ShouldThrowFaultException_WhenDatabaseFails()
        {
            // Arrange
            string nickname = "ErrorUser";

            mockRepo.Setup(repo => repo.IsNicknameInUse(nickname))
                    .ThrowsAsync(new EntityException("Connection failed"));

            // Act
            FaultException<ServiceErrorDetailDTO> exception = await 
                Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => accountManager.IsNicknameInUse(nickname)
            );

            var expected = new
            {
                ErrorCode = "DATABASE_ERROR",
                RepoCalls = 1
            };

            var actual = new
            {
                ErrorCode = exception.Detail.ErrorCode,
                RepoCalls = mockRepo.Invocations.Count(i => 
                i.Method.Name == nameof(IAccountRepository.IsNicknameInUse))
            };

            // Assert
            Assert.Equal(expected, actual);
        }
    }
}
