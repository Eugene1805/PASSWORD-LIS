using Moq;
using Services.Util;

namespace Test.ServicesTests.ServicesUtilTest
{
    public class NotificationServiceTests
    {
        private readonly Mock<IEmailSender> mockEmailSender;
        private readonly INotificationService notificationService;

        public NotificationServiceTests()
        {
            // Arrange (Setup global para todas las pruebas)
            mockEmailSender = new Mock<IEmailSender>();
            notificationService = new NotificationService(mockEmailSender.Object);
        }

        [Fact]
        public async Task SendAccountVerificationEmailAsync_ShouldCallSendEmailAsync_WithCorrectParameters()
        {
            // Arrange
            var testEmail = "test@example.com";
            var testCode = "123456";
            var expectedSubject = "Código de Verificación de Cuenta";

            // Act
            await notificationService.SendAccountVerificationEmailAsync(testEmail, testCode);

            // Assert
            // Verificamos que el método SendEmailAsync fue llamado exactamente una vez.
            mockEmailSender.Verify(
                sender => sender.SendEmailAsync(
                    // Verificamos que el email del destinatario sea el correcto.
                    It.Is<string>(email => email == testEmail),
                    // Verificamos que el asunto sea el esperado.
                    It.Is<string>(subject => subject == expectedSubject),
                    // Verificamos que el cuerpo del mensaje contenga el código de verificación.
                    It.Is<string>(body => body.Contains(testCode))
                ),
                Times.Once()
            );
        }

        [Fact]
        public async Task SendPasswordResetEmailAsync_ShouldCallSendEmailAsync_WithCorrectParameters()
        {
            // Arrange
            var testEmail = "user@domain.com";
            var testCode = "ABCDEF";
            var expectedSubject = "Restablecimiento de Contraseña";

            // Act
            await notificationService.SendPasswordResetEmailAsync(testEmail, testCode);

            // Assert
            // Verificamos de nuevo, pero para el método de reseteo de contraseña.
            mockEmailSender.Verify(
                sender => sender.SendEmailAsync(
                    // Verificamos el email.
                    It.Is<string>(email => email == testEmail),
                    // Verificamos el asunto.
                    It.Is<string>(subject => subject == expectedSubject),
                    // Verificamos que el cuerpo contenga el código de reseteo.
                    It.Is<string>(body => body.Contains(testCode))
                ),
                Times.Once()
            );
        }
    }
}
