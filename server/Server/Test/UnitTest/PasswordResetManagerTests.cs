using Data.DAL.Interfaces;
using Moq;
using Services.Contracts.DTOs;
using Services.Services;
using Services.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.UnitTest
{
    public class PasswordResetManagerTests
    {
        private readonly Mock<IAccountRepository> _mockRepo;
        private readonly Mock<INotificationService> _mockNotification;
        private readonly Mock<IVerificationCodeService> _mockCodeService;
        private readonly PasswordResetManager _passwordManager;

        public PasswordResetManagerTests()
        {
            // Arrange
            _mockRepo = new Mock<IAccountRepository>();
            _mockNotification = new Mock<INotificationService>();
            _mockCodeService = new Mock<IVerificationCodeService>();

            _passwordManager = new PasswordResetManager(
                _mockRepo.Object,
                _mockNotification.Object,
                _mockCodeService.Object
            );
        }

        [Fact]
        public async Task ResetPassword_ShouldReturnTrue_WhenCodeIsValid()
        {
            // Arrange
            var resetDto = new PasswordResetDTO { Email = "test@example.com", ResetCode = "123456", NewPassword = "NewPassword123!" };

            // Configuramos los mocks para el escenario de éxito
            _mockCodeService.Setup(s => s.ValidateCode(resetDto.Email, resetDto.ResetCode, CodeType.PasswordReset)).Returns(true);
            _mockRepo.Setup(r => r.ResetPassword(resetDto.Email, It.IsAny<string>())).Returns(true);

            // Act
            var result =  _passwordManager.ResetPassword(resetDto);

            // Assert
            Assert.True(result);

            // Verificamos que se llamó al repositorio para cambiar la contraseña
            _mockRepo.Verify(r => r.ResetPassword(resetDto.Email, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ResetPassword_ShouldReturnFalse_WhenCodeIsInvalid()
        {
            // Arrange
            var resetDto = new PasswordResetDTO { Email = "test@example.com", ResetCode = "invalid-code", NewPassword = "NewPassword123!" };

            // Configuramos el mock para que el código sea inválido
            _mockCodeService.Setup(s => s.ValidateCode(resetDto.Email, resetDto.ResetCode, CodeType.PasswordReset)).Returns(false);

            // Act
            var result =  _passwordManager.ResetPassword(resetDto);

            // Assert
            Assert.False(result);

            // Verificamos que NUNCA se intentó cambiar la contraseña en la base de datos si el código era malo.
            _mockRepo.Verify(r => r. ResetPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RequestPasswordResetCode_ShouldReturnFalse_WhenUserCannotRequestCode()
        {
            // Arrange
            var emailDto = new EmailVerificationDTO { Email = "test@example.com" };

            // Configuramos el mock para simular que el usuario no puede pedir código (throttling)
            _mockRepo.Setup(r => r.AccountAlreadyExist(emailDto.Email)).Returns(true); // El usuario sí existe
            _mockCodeService.Setup(s => s.CanRequestCode(emailDto.Email, CodeType.PasswordReset)).Returns(false); // Pero no puede pedir código

            // Act
            var result =  _passwordManager.RequestPasswordResetCode(emailDto);

            // Assert
            Assert.False(result);

            // Verificamos que no se generó un nuevo código ni se envió un email.
            _mockCodeService.Verify(s => s.GenerateAndStoreCode(It.IsAny<string>(), It.IsAny<CodeType>()), Times.Never);
            _mockNotification.Verify(s => s.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}
