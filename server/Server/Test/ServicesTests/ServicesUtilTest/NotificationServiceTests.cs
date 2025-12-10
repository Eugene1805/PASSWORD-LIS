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

            mockEmailSender = new Mock<IEmailSender>();
            notificationService = new NotificationService(mockEmailSender.Object);
        }

        [Fact]
        public async Task SendAccountVerificationEmailAsync_ShouldCallSendEmailAsync_WithCorrectParameters()
        {

            var testEmail = "test@example.com";
            var testCode = "123456";
            var expectedSubject = "Código de Verificación de Cuenta";


            await notificationService.SendAccountVerificationEmailAsync(testEmail, testCode);


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

            var testEmail = "user@domain.com";
            var testCode = "ABCDEF";
            var expectedSubject = "Restablecimiento de Contraseña";


            await notificationService.SendPasswordResetEmailAsync(testEmail, testCode);


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
