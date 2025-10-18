using Data.DAL.Interfaces;
using Moq;
using Services.Contracts.DTOs;
using Services.Services;
using Services.Util;

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
            var resetDto = new PasswordResetDTO { Email = "test@example.com", ResetCode = "123456", NewPassword = "NewPassword123!" };

            // Configuramos los mocks para el escenario de éxito
            mockCodeService.Setup(s => s.ValidateCode(resetDto.Email, resetDto.ResetCode, CodeType.PasswordReset,true)).Returns(true);
            mockRepo.Setup(r => r.ResetPassword(resetDto.Email, It.IsAny<string>())).Returns(true);

            // Act
            var result = passwordManager.ResetPassword(resetDto);

            // Assert
            Assert.True(result);

            // Verificamos que se llamó al repositorio para cambiar la contraseña
            mockRepo.Verify(r => r.ResetPassword(resetDto.Email, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void ResetPassword_ShouldReturnFalse_WhenCodeIsInvalid()
        {
            // Arrange
            var resetDto = new PasswordResetDTO { Email = "test@example.com", ResetCode = "invalid-code", NewPassword = "NewPassword123!" };

            // Configuramos el mock para que el código sea inválido
            mockCodeService.Setup(s => s.ValidateCode(resetDto.Email, resetDto.ResetCode, CodeType.PasswordReset,true)).Returns(false);

            // Act
            var result =  passwordManager.ResetPassword(resetDto);

            // Assert
            Assert.False(result);

            // Verificamos que NUNCA se intentó cambiar la contraseña en la base de datos si el código era malo.
            mockRepo.Verify(r => r. ResetPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void RequestPasswordResetCode_ShouldReturnFalse_WhenUserCannotRequestCode()
        {
            // Arrange
            var emailDto = new EmailVerificationDTO { Email = "test@example.com" };

            // Configuramos el mock para simular que el usuario no puede pedir código (throttling)
            mockRepo.Setup(r => r.AccountAlreadyExist(emailDto.Email)).Returns(true); // El usuario sí existe
            mockCodeService.Setup(s => s.CanRequestCode(emailDto.Email, CodeType.PasswordReset)).Returns(false); // Pero no puede pedir código

            // Act
            var result = passwordManager.RequestPasswordResetCode(emailDto);

            // Assert
            Assert.False(result);

            // Verificamos que no se generó un nuevo código ni se envió un email.
            mockCodeService.Verify(s => s.GenerateAndStoreCode(It.IsAny<string>(), It.IsAny<CodeType>()), Times.Never);
            mockNotification.Verify(s => s.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}
