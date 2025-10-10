using Data.DAL.Interfaces;
using Data.Model;
using Moq;
using Services.Contracts.DTOs;
using Services.Services;
using Services.Util;

namespace Test.UnitTest
{
    public class AccountManagerTests
    {
        private readonly Mock<IAccountRepository> _mockRepo;
        private readonly Mock<INotificationService> _mockNotification;
        private readonly Mock<IVerificationCodeService> _mockCodeService;
        private readonly AccountManager _accountManager;

        public AccountManagerTests()
        {
            // Arrange común para todas las pruebas
            _mockRepo = new Mock<IAccountRepository>();
            _mockNotification = new Mock<INotificationService>();
            _mockCodeService = new Mock<IVerificationCodeService>();

            // Inyectamos los mocks al servicio que vamos a probar
            _accountManager = new AccountManager(
                _mockRepo.Object,
                _mockNotification.Object,
                _mockCodeService.Object
            );
        }

        [Fact]
        public async Task CreateAccount_ShouldReturnTrueAndSendEmail_WhenAccountIsCreatedSuccessfully()
        {
            // Arrange
            var newAccountDto = new NewAccountDTO { Email = "test@example.com", Password = "Password123!" };
            var generatedCode = "123456";

            // Configuramos los mocks para el "camino feliz"
            _mockRepo.Setup(repo => repo.CreateAccount(It.IsAny<UserAccount>())).Returns(true);
            _mockCodeService.Setup(service => service.GenerateAndStoreCode(newAccountDto.Email, CodeType.EmailVerification)).Returns(generatedCode);

            // Act
            var result =  _accountManager.CreateAccount(newAccountDto);

            // Assert
            Assert.True(result);

            // Verificamos que los métodos importantes de nuestras dependencias fueron llamados.
            _mockRepo.Verify(repo => repo.CreateAccount(It.IsAny<UserAccount>()), Times.Once);
            _mockCodeService.Verify(service => service.GenerateAndStoreCode(newAccountDto.Email, CodeType.EmailVerification), Times.Once);
            _mockNotification.Verify(service => service.SendAccountVerificationEmailAsync(newAccountDto.Email, generatedCode), Times.Once);
        }

        [Fact]
        public async Task CreateAccount_ShouldReturnFalse_WhenRepositoryFailsToCreateAccount()
        {
            // Arrange
            var newAccountDto = new NewAccountDTO { Email = "test@example.com", Password = "Password123!" };

            // Configuramos el mock del repositorio para que falle la creación.
            _mockRepo.Setup(repo => repo.CreateAccount(It.IsAny<UserAccount>())).Returns(false);

            // Act
            var result =  _accountManager.CreateAccount(newAccountDto);

            // Assert
            Assert.False(result);

            // Verificamos que, como la creación falló, NUNCA se intentó generar un código ni enviar un email.
            // Esto prueba que la lógica de nuestro servicio es correcta.
            _mockCodeService.Verify(service => service.GenerateAndStoreCode(It.IsAny<string>(), It.IsAny<CodeType>()), Times.Never);
            _mockNotification.Verify(service => service.SendAccountVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}
