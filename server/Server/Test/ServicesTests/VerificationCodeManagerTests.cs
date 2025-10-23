using Data.DAL.Interfaces;
using Moq;
using Services.Contracts.DTOs;
using Services.Services;
using Services.Util;

namespace Test.ServicesTests
{
    public class VerificationCodeManagerTests
    {
        private readonly Mock<IAccountRepository> mockAccountRepository;
        private readonly Mock<INotificationService> mockNotificationService;
        private readonly Mock<IVerificationCodeService> mockCodeService;
        private readonly VerificationCodeManager verificationCodeManager;

        public VerificationCodeManagerTests()
        {
            mockAccountRepository = new Mock<IAccountRepository>();
            mockNotificationService = new Mock<INotificationService>();
            mockCodeService = new Mock<IVerificationCodeService>();

            verificationCodeManager = new VerificationCodeManager(
                mockAccountRepository.Object,
                mockNotificationService.Object,
                mockCodeService.Object
            );
        }

        [Fact]
        public void VerifyEmail_WhenCodeIsValid_ShouldCallRepositoryAndReturnTrue()
        {
            // Arrange
            var verificationDto = new EmailVerificationDTO { Email = "test@example.com", VerificationCode = "123456" };

            mockCodeService.Setup(s => s.ValidateCode(verificationDto.Email, verificationDto.VerificationCode, CodeType.EmailVerification,true))
                            .Returns(true);

            mockAccountRepository.Setup(repo => repo.VerifyEmail(verificationDto.Email)).Returns(true);

            // Act
            var result = verificationCodeManager.VerifyEmail(verificationDto);

            // Assert
            Assert.True(result);
            mockCodeService.Verify(s => s.ValidateCode(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CodeType>(),true), Times.Once);
            mockAccountRepository.Verify(repo => repo.VerifyEmail(verificationDto.Email), Times.Once);
        }

        [Fact]
        public void VerifyEmail_WhenCodeIsInvalid_ShouldReturnFalseAndNotCallRepository()
        {
            // Arrange
            var verificationDto = new EmailVerificationDTO { Email = "test@example.com", VerificationCode = "wrong-code" };

            mockCodeService.Setup(s => s.ValidateCode(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CodeType>(),true))
                            .Returns(false);

            // Act
            var result = verificationCodeManager.VerifyEmail(verificationDto);

            // Assert
            Assert.False(result);
            mockAccountRepository.Verify(repo => repo.VerifyEmail(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void ResendVerificationCode_WhenCanRequestCode_ShouldGenerateAndSendEmail()
        {
            // Arrange
            var email = "user@example.com";
            var generatedCode = "654321";

            mockCodeService.Setup(s => s.CanRequestCode(email, CodeType.EmailVerification)).Returns(true);
            
            mockCodeService.Setup(s => s.GenerateAndStoreCode(email, CodeType.EmailVerification)).Returns(generatedCode);

            // Act
            var result = verificationCodeManager.ResendVerificationCode(email);

            // Assert
            Assert.True(result);
            mockCodeService.Verify(s => s.GenerateAndStoreCode(email, CodeType.EmailVerification), Times.Once);
            mockNotificationService.Verify(n => n.SendAccountVerificationEmailAsync(email, generatedCode), Times.Once);
        }

        [Fact]
        public void ResendVerificationCode_WhenCannotRequestCode_ShouldReturnFalseAndDoNothing()
        {
            // Arrange
            var email = "user@example.com";

            mockCodeService.Setup(s => s.CanRequestCode(email, CodeType.EmailVerification)).Returns(false);

            // Act
            var result = verificationCodeManager.ResendVerificationCode(email);

            // Assert
            Assert.False(result);
            mockCodeService.Verify(s => s.GenerateAndStoreCode(It.IsAny<string>(), It.IsAny<CodeType>()), Times.Never);
            mockNotificationService.Verify(n => n.SendAccountVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}
