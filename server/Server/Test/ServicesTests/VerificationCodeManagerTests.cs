using Data.DAL.Interfaces;
using Moq;
using Services.Contracts.DTOs;
using Services.Services;
using Services.Util;
using System.ServiceModel;

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

            mockCodeService.Setup(s => s.ValidateCode(verificationDto.Email, verificationDto.VerificationCode,
                CodeType.EmailVerification,true)).Returns(true);

            mockAccountRepository.Setup(repo => repo.VerifyEmail(verificationDto.Email)).Returns(true);

            // Act
            var result = verificationCodeManager.VerifyEmail(verificationDto);

            // Assert
            Assert.True(result);
            mockCodeService.Verify(s => s.ValidateCode(It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<CodeType>(),true), Times.Once);
            mockAccountRepository.Verify(repo => repo.VerifyEmail(verificationDto.Email), Times.Once);
        }

        [Fact]
        public void VerifyEmail_WhenCodeIsInvalid_ShouldReturnFalseAndNotCallRepository()
        {
            // Arrange
            var verificationDto = new EmailVerificationDTO { Email = "test@example.com", 
                VerificationCode = "wrong-code" };

            mockCodeService.Setup(s => s.ValidateCode(It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<CodeType>(),true)).Returns(false);

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
            
            mockCodeService.Setup(s => s.GenerateAndStoreCode(email,
                CodeType.EmailVerification)).Returns(generatedCode);

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
            mockNotificationService.Verify(n => n.SendAccountVerificationEmailAsync(It.IsAny<string>(),
                It.IsAny<string>()), Times.Never);
        }


        [Fact]
        public void VerifyEmail_WhenValidationThrows_ShouldBubbleExceptionAndNotCallRepository()
        {
            // Arrange
            var dto = new EmailVerificationDTO { Email = "e@x.com", VerificationCode = "X" };
            mockCodeService.Setup(s => s.ValidateCode(dto.Email, dto.VerificationCode, CodeType.EmailVerification, 
                true)).Throws(new Exception("validation failure"));

            // Act + Assert
            Assert.Throws<FaultException<ServiceErrorDetailDTO>>(() => verificationCodeManager.VerifyEmail(dto));
            mockAccountRepository.Verify(r => r.VerifyEmail(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void VerifyEmail_WhenRepositoryThrows_ShouldBubbleException()
        {
            // Arrange
            var dto = new EmailVerificationDTO { Email = "e@x.com", VerificationCode = "OK" };
            mockCodeService.Setup(s => s.ValidateCode(dto.Email, dto.VerificationCode, CodeType.EmailVerification,
                true)).Returns(true);
            mockAccountRepository.Setup(r => r.VerifyEmail(dto.Email)).Throws(new Exception("db failure"));

            // Act + Assert
            Assert.Throws<FaultException<ServiceErrorDetailDTO>>(() => verificationCodeManager.VerifyEmail(dto));
        }

        [Fact]
        public void VerifyEmail_WhenRepositoryReturnsFalse_ShouldReturnFalse()
        {
            // Arrange
            var dto = new EmailVerificationDTO { Email = "e@x.com", VerificationCode = "OK" };
            mockCodeService.Setup(s => s.ValidateCode(dto.Email, dto.VerificationCode, CodeType.EmailVerification,
                true)).Returns(true);
            mockAccountRepository.Setup(r => r.VerifyEmail(dto.Email)).Returns(false);

            // Act
            var result = verificationCodeManager.VerifyEmail(dto);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ResendVerificationCode_WhenGenerationFails_ShouldBubbleException()
        {
            // Arrange
            var email = "user@example.com";
            mockCodeService.Setup(s => s.CanRequestCode(email, CodeType.EmailVerification)).Returns(true);
            mockCodeService.Setup(s => s.GenerateAndStoreCode(email, CodeType.EmailVerification))
                            .Throws(new Exception("storage down"));

            // Act + Assert
            Assert.Throws<FaultException<ServiceErrorDetailDTO>>(() => verificationCodeManager.ResendVerificationCode(email));
            mockNotificationService.Verify(n => n.SendAccountVerificationEmailAsync(It.IsAny<string>(), 
                It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void ResendVerificationCode_WhenNotificationTaskFaults_ShouldReturnTrue()
        {
            // Arrange
            var email = "user@example.com";
            mockCodeService.Setup(s => s.CanRequestCode(email, CodeType.EmailVerification)).Returns(true);
            mockCodeService.Setup(s => s.GenerateAndStoreCode(email, CodeType.EmailVerification)).Returns("C0DE");
            mockNotificationService.Setup(n => n.SendAccountVerificationEmailAsync(email, It.IsAny<string>()))
                                   .Returns(Task.FromException(new System.Exception("smtp failure")));

            // Act
            var result = verificationCodeManager.ResendVerificationCode(email);

            // Assert
            Assert.True(result);
            mockNotificationService.Verify(n => n.SendAccountVerificationEmailAsync(email, It.IsAny<string>()),
                Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ResendVerificationCode_WithNullOrEmptyEmail_ShouldReturnFalse_WhenCannotRequest(string? email)
        {
            // Arrange
            mockCodeService.Setup(s => s.CanRequestCode(email!, CodeType.EmailVerification)).Returns(false);

            // Act
            var result = verificationCodeManager.ResendVerificationCode(email!);

            // Assert
            Assert.False(result);
            mockCodeService.Verify(s => s.GenerateAndStoreCode(It.IsAny<string>(), It.IsAny<CodeType>()), Times.Never);
            mockNotificationService.Verify(n => n.SendAccountVerificationEmailAsync(It.IsAny<string>(), 
                It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void VerifyEmail_WithNullOrEmptyCode_ShouldReturnFalse(string? code)
        {
            // Arrange
            var dto = new EmailVerificationDTO { Email = "user@example.com", VerificationCode = code! };
            mockCodeService.Setup(s => s.ValidateCode(dto.Email, dto.VerificationCode, CodeType.EmailVerification,
                true)).Returns(false);

            // Act
            var result = verificationCodeManager.VerifyEmail(dto);

            // Assert
            Assert.False(result);
            mockAccountRepository.Verify(r => r.VerifyEmail(It.IsAny<string>()), Times.Never);
        }
    }
}
