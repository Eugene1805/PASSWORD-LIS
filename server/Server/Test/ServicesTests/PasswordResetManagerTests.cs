using Data.DAL.Interfaces;
using Moq;
using Services.Contracts.DTOs;
using Services.Services;
using Services.Util;
using System.ServiceModel;

namespace Test.ServicesTests
{
    public class PasswordResetManagerTests
    {
        private readonly Mock<IAccountRepository> mockRepo;
        private readonly Mock<INotificationService> mockNotification;
        private readonly Mock<IVerificationCodeService> mockCodeService;
        private readonly PasswordResetManager passwordManager;

        public PasswordResetManagerTests()
        {
            // Arrange
            mockRepo = new Mock<IAccountRepository>();
            mockNotification = new Mock<INotificationService>();
            mockCodeService = new Mock<IVerificationCodeService>();

            passwordManager = new PasswordResetManager(
                mockRepo.Object,
                mockNotification.Object,
                mockCodeService.Object
            );
        }

        [Fact]
        public void ResetPassword_ShouldReturnTrue_WhenCodeIsValid()
        {
            // Arrange
            var resetDto = new PasswordResetDTO { Email = "test@example.com", ResetCode = "123456", 
                NewPassword = "NewPassword123!" };

            mockCodeService.Setup(s => s.ValidateCode(resetDto.Email, 
                resetDto.ResetCode, CodeType.PasswordReset, true)).Returns(true);
            mockRepo.Setup(r => r.ResetPassword(resetDto.Email, It.IsAny<string>())).Returns(true);

            // Act
            var result = passwordManager.ResetPassword(resetDto);

            // Assert
            Assert.True(result);
            mockRepo.Verify(r => r.ResetPassword(resetDto.Email, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void ResetPassword_ShouldReturnFalse_WhenCodeIsInvalid()
        {
            // Arrange
            var resetDto = new PasswordResetDTO { Email = "test@example.com", ResetCode = "invalid-code",
                NewPassword = "NewPassword123!" };

            mockCodeService.Setup(s => s.ValidateCode(resetDto.Email, 
                resetDto.ResetCode, CodeType.PasswordReset, true)).Returns(false);

            // Act
            var result = passwordManager.ResetPassword(resetDto);

            // Assert
            Assert.False(result);

            mockRepo.Verify(r => r. ResetPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void RequestPasswordResetCode_ShouldReturnFalse_WhenUserCannotRequestCode()
        {
            // Arrange
            var emailDto = new EmailVerificationDTO { Email = "test@example.com" };

            mockRepo.Setup(r => r.AccountAlreadyExist(emailDto.Email)).Returns(true); 
            mockCodeService.Setup(s => s.CanRequestCode(emailDto.Email, CodeType.PasswordReset)).Returns(false);

            // Act
            var result = passwordManager.RequestPasswordResetCode(emailDto);

            // Assert
            Assert.False(result);

            mockCodeService.Verify(s => s.GenerateAndStoreCode(It.IsAny<string>(), It.IsAny<CodeType>()), Times.Never);
            mockNotification.Verify(s => s.SendPasswordResetEmailAsync(It.IsAny<string>(),
                It.IsAny<string>()), Times.Never);
        }


        [Fact]
        public void RequestPasswordResetCode_ShouldReturnFalse_WhenAccountDoesNotExist()
        {
            // Arrange
            var emailDto = new EmailVerificationDTO { Email = "missing@example.com" };
            mockRepo.Setup(r => r.AccountAlreadyExist(emailDto.Email)).Returns(false);

            // Act
            var result = passwordManager.RequestPasswordResetCode(emailDto);

            // Assert
            Assert.False(result);
            mockCodeService.Verify(s => s.GenerateAndStoreCode(It.IsAny<string>(), It.IsAny<CodeType>()), Times.Never);
            mockNotification.Verify(s => s.SendPasswordResetEmailAsync(It.IsAny<string>(), 
                It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void RequestPasswordResetCode_ShouldReturnTrue_AndSendEmail_WhenAllowed()
        {
            // Arrange
            var emailDto = new EmailVerificationDTO { Email = "user@example.com" };
            const string generated = "ABC123";

            mockRepo.Setup(r => r.AccountAlreadyExist(emailDto.Email)).Returns(true);
            mockCodeService.Setup(s => s.CanRequestCode(emailDto.Email, CodeType.PasswordReset)).Returns(true);
            mockCodeService.Setup(s => s.GenerateAndStoreCode(emailDto.Email, 
                CodeType.PasswordReset)).Returns(generated);

            // Act
            var result = passwordManager.RequestPasswordResetCode(emailDto);

            // Assert
            Assert.True(result);
            mockCodeService.Verify(s => s.GenerateAndStoreCode(emailDto.Email, CodeType.PasswordReset), Times.Once);
            mockNotification.Verify(s => s.SendPasswordResetEmailAsync(emailDto.Email, generated), Times.Once);
        }

        [Fact]
        public void RequestPasswordResetCode_ShouldBubbleException_WhenCodeGenerationFails()
        {
            // Arrange
            var emailDto = new EmailVerificationDTO { Email = "user@example.com" };
            mockRepo.Setup(r => r.AccountAlreadyExist(emailDto.Email)).Returns(true);
            mockCodeService.Setup(s => s.CanRequestCode(emailDto.Email, CodeType.PasswordReset)).Returns(true);
            mockCodeService.Setup(s => s.GenerateAndStoreCode(emailDto.Email, CodeType.PasswordReset))
                .Throws(new Exception("storage unavailable"));

            // Act + Assert
            Assert.Throws<FaultException<ServiceErrorDetailDTO>>(() => passwordManager.RequestPasswordResetCode(emailDto));
            mockNotification.Verify(s => s.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public void RequestPasswordResetCode_ShouldNotThrow_WhenNotificationTaskFaults()
        {
            // Arrange
            var emailDto = new EmailVerificationDTO { Email = "user@example.com" };
            mockRepo.Setup(r => r.AccountAlreadyExist(emailDto.Email)).Returns(true);
            mockCodeService.Setup(s => s.CanRequestCode(emailDto.Email, CodeType.PasswordReset)).Returns(true);
            mockCodeService.Setup(s => s.GenerateAndStoreCode(emailDto.Email, CodeType.PasswordReset)).Returns("C0DE");

            mockNotification.Setup(n => n.SendPasswordResetEmailAsync(emailDto.Email, It.IsAny<string>()))
                .Returns(Task.FromException(new Exception("smtp failure")));

            // Act
            var result = passwordManager.RequestPasswordResetCode(emailDto);

            // Assert
            Assert.True(result);
            mockNotification.Verify(n => n.SendPasswordResetEmailAsync(emailDto.Email, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void ResetPassword_ShouldReturnFalse_WhenRepositoryUpdateFails()
        {
            // Arrange
            var dto = new PasswordResetDTO { Email = "user@example.com", ResetCode = "OK", NewPassword = "P@ssw0rd!" };
            mockCodeService.Setup(s => s.ValidateCode(dto.Email, dto.ResetCode, 
                CodeType.PasswordReset, true)).Returns(true);
            mockRepo.Setup(r => r.ResetPassword(dto.Email, It.IsAny<string>())).Returns(false);

            // Act
            var result = passwordManager.ResetPassword(dto);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ResetPassword_ShouldBubbleException_WhenValidationThrows()
        {
            // Arrange
            var dto = new PasswordResetDTO { Email = "err@example.com", ResetCode = "X", NewPassword = "P@ssw0rd!" };
            mockCodeService.Setup(s => s.ValidateCode(dto.Email, dto.ResetCode, CodeType.PasswordReset, true))
                .Throws(new Exception("validation service down"));

            // Act + Assert
            Assert.Throws<FaultException<ServiceErrorDetailDTO>>(() => passwordManager.ResetPassword(dto));
            mockRepo.Verify(r => r.ResetPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void ResetPassword_ShouldHashPassword_BeforeSaving()
        {
            // Arrange
            var dto = new PasswordResetDTO { Email = "user@example.com", ResetCode = "123456",
                NewPassword = "PlainText#1" };
            string? capturedHash = null;
            mockCodeService.Setup(s => s.ValidateCode(dto.Email, dto.ResetCode,
                CodeType.PasswordReset, true)).Returns(true);
            mockRepo.Setup(r => r.ResetPassword(dto.Email, It.IsAny<string>()))
                .Callback<string, string>((email, hash) => capturedHash = hash).Returns(true);

            // Act
            var result = passwordManager.ResetPassword(dto);

            
            var expected = new { Result = true, CapturedNotNull = true, Different = true, Verifies = true };
            var actual = new
            {
                Result = result,
                CapturedNotNull = capturedHash != null,
                Different = capturedHash != null && capturedHash != dto.NewPassword,
                Verifies = capturedHash != null && BCrypt.Net.BCrypt.Verify(dto.NewPassword, capturedHash)
            };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ValidatePasswordResetCode_ShouldNotConsumeCode()
        {
            // Arrange
            var emailDto = new EmailVerificationDTO { Email = "user@example.com", VerificationCode = "123456" };
            mockCodeService.Setup(s => s.ValidateCode(emailDto.Email, emailDto.VerificationCode, 
                CodeType.PasswordReset, false))
                .Returns(true);

            // Act
            var result = passwordManager.ValidatePasswordResetCode(emailDto);

            // Assert
            Assert.True(result);
            mockCodeService.Verify(s => s.ValidateCode(emailDto.Email, emailDto.VerificationCode,
                CodeType.PasswordReset, false), Times.Once);
        }

        [Fact]
        public void ValidatePasswordResetCode_ShouldReturnFalse_WhenInvalid()
        {
            // Arrange
            var emailDto = new EmailVerificationDTO { Email = "user@example.com", VerificationCode = "bad" };
            mockCodeService.Setup(s => s.ValidateCode(emailDto.Email, emailDto.VerificationCode, 
                CodeType.PasswordReset, false))
                .Returns(false);

            // Act
            var result = passwordManager.ValidatePasswordResetCode(emailDto);

            // Assert
            Assert.False(result);
        }
    }
}
