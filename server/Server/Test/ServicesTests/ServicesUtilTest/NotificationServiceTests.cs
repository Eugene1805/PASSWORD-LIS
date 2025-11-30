using Moq;
using Services.Util;

namespace Test.ServicesTests.ServicesUtilTest
{
    public class NotificationServiceTests
    {
        private readonly Mock<IEmailSender> mockEmailSender;
        private readonly NotificationService notificationService;

        public NotificationServiceTests()
        {
            // Arrange
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
            mockEmailSender.Verify(
                sender => sender.SendEmailAsync(
                    It.Is<string>(email => email == testEmail),
                    It.Is<string>(subject => subject == expectedSubject),
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
            mockEmailSender.Verify(
                sender => sender.SendEmailAsync(
                    It.Is<string>(email => email == testEmail),
                    It.Is<string>(subject => subject == expectedSubject),
                    It.Is<string>(body => body.Contains(testCode))
                ),
                Times.Once()
            );
        }
    }
}
