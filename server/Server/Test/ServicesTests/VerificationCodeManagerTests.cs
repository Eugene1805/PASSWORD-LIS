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
            // Arrange (Configuración común)
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

            // El servicio de código dice que el código es válido
            mockCodeService.Setup(s => s.ValidateCode(verificationDto.Email, verificationDto.VerificationCode, CodeType.EmailVerification,true))
                            .Returns(true);

            // El repositorio dice que la verificación en BD fue exitosa
            mockAccountRepository.Setup(repo => repo.VerifyEmail(verificationDto.Email)).Returns(true);

            // Act
            var result = verificationCodeManager.VerifyEmail(verificationDto);

            // Assert
            Assert.True(result);
            // Verificamos que se llamó al servicio de código
            mockCodeService.Verify(s => s.ValidateCode(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CodeType>(),true), Times.Once);
            // Verificamos que, como el código era válido, SÍ se llamó al repositorio
            mockAccountRepository.Verify(repo => repo.VerifyEmail(verificationDto.Email), Times.Once);
        }

        [Fact]
        public void VerifyEmail_WhenCodeIsInvalid_ShouldReturnFalseAndNotCallRepository()
        {
            // Arrange
            var verificationDto = new EmailVerificationDTO { Email = "test@example.com", VerificationCode = "wrong-code" };

            // El servicio de código dice que el código NO es válido
            mockCodeService.Setup(s => s.ValidateCode(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CodeType>(),true))
                            .Returns(false);

            // Act
            var result = verificationCodeManager.VerifyEmail(verificationDto);

            // Assert
            Assert.False(result);
            // Verificamos que el repositorio NUNCA fue llamado, porque la validación del código falló primero.
            mockAccountRepository.Verify(repo => repo.VerifyEmail(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void ResendVerificationCode_WhenCanRequestCode_ShouldGenerateAndSendEmail()
        {
            // Arrange
            var email = "user@example.com";
            var generatedCode = "654321";

            // El usuario SÍ puede solicitar un nuevo código
            mockCodeService.Setup(s => s.CanRequestCode(email, CodeType.EmailVerification)).Returns(true);
            // Configuramos el código que se va a "generar"
            mockCodeService.Setup(s => s.GenerateAndStoreCode(email, CodeType.EmailVerification)).Returns(generatedCode);

            // Act
            var result = verificationCodeManager.ResendVerificationCode(email);

            // Assert
            Assert.True(result);
            // Verificamos que se generó un código nuevo
            mockCodeService.Verify(s => s.GenerateAndStoreCode(email, CodeType.EmailVerification), Times.Once);
            // Verificamos que se intentó enviar el email con el código generado
            mockNotificationService.Verify(n => n.SendAccountVerificationEmailAsync(email, generatedCode), Times.Once);
        }

        [Fact]
        public void ResendVerificationCode_WhenCannotRequestCode_ShouldReturnFalseAndDoNothing()
        {
            // Arrange
            var email = "user@example.com";

            // El usuario NO puede solicitar un nuevo código (throttling)
            mockCodeService.Setup(s => s.CanRequestCode(email, CodeType.EmailVerification)).Returns(false);

            // Act
            var result = verificationCodeManager.ResendVerificationCode(email);

            // Assert
            Assert.False(result);
            // Verificamos que NO se generó un código nuevo
            mockCodeService.Verify(s => s.GenerateAndStoreCode(It.IsAny<string>(), It.IsAny<CodeType>()), Times.Never);
            // Verificamos que NO se intentó enviar ningún email
            mockNotificationService.Verify(n => n.SendAccountVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}
