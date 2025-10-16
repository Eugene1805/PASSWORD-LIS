using Data.DAL.Interfaces;
using Data.Exceptions;
using Data.Model;
using Moq;
using System.ServiceModel;
using Services.Contracts.DTOs;
using Services.Services;
using Services.Util;

namespace Test.ServicesTests
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
        public async Task CreateAccount_ShouldCompleteSuccessfully_WhenDependenciesSucceed()
        {
            // Arrange
            var newAccountDto = new NewAccountDTO { Email = "test@example.com", Password = "Password123!" };
            var generatedCode = "123456";

            // CAMBIO 1: El mock del repositorio ahora devuelve una tarea completada,
            // simulando un método async que termina sin errores.
            _mockRepo.Setup(repo => repo.CreateAccountAsync(It.IsAny<UserAccount>()))
                     .Returns(Task.CompletedTask);

            _mockCodeService.Setup(service => service.GenerateAndStoreCode(newAccountDto.Email, CodeType.EmailVerification))
                            .Returns(generatedCode);

            // Act
            // CAMBIO 2: Ahora usamos 'await'. Si el método lanza una excepción, esta línea fallará la prueba.
            // Esto reemplaza al Assert.True(). El éxito es la ausencia de una excepción.
            await _accountManager.CreateAccountAsync(newAccountDto);

            // Assert
            // La aserción principal es que la línea de arriba no falló.
            // Ahora solo verificamos que las dependencias fueron llamadas como se esperaba.
            _mockRepo.Verify(repo => repo.CreateAccountAsync(It.IsAny<UserAccount>()), Times.Once);
            _mockCodeService.Verify(service => service.GenerateAndStoreCode(newAccountDto.Email, CodeType.EmailVerification), Times.Once);
            _mockNotification.Verify(service => service.SendAccountVerificationEmailAsync(newAccountDto.Email, generatedCode), Times.Once);
        }

        [Fact]
        public async Task CreateAccount_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var newAccountDto = new NewAccountDTO
            {
                Email = "newuser@example.com",
                Password = "AValidPassword123!",
                Nickname = "Newbie",
                FirstName = "Test",
                LastName = "User"
            };
            var verificationCode = "123456";

            _mockRepo.Setup(repo => repo.CreateAccountAsync(It.IsAny<UserAccount>()))
                     .Returns(Task.CompletedTask);

            _mockCodeService.Setup(s => s.GenerateAndStoreCode(newAccountDto.Email, CodeType.EmailVerification))
                            .Returns(verificationCode);

            // Act
            await _accountManager.CreateAccountAsync(newAccountDto);

            // Assert
            // Verifica que se intentó crear la cuenta en la base de datos
            _mockRepo.Verify(repo => repo.CreateAccountAsync(It.IsAny<UserAccount>()), Times.Once);

            // Verifica que se generó un código de verificación
            _mockCodeService.Verify(s => s.GenerateAndStoreCode(newAccountDto.Email, CodeType.EmailVerification), Times.Once);

            // Verifica que se intentó enviar el email de notificación
            _mockNotification.Verify(n => n.SendAccountVerificationEmailAsync(newAccountDto.Email, verificationCode), Times.Once);
        }


        [Fact]
        public async Task CreateAccount_ShouldThrowFaultException_WhenAccountAlreadyExists()
        {
            // Arrange
            var newAccountDto = new NewAccountDTO
            {
                Email = "duplicate@example.com",
                Password = "AValidPassword123!" // Dato necesario para que BCrypt no falle
            };

            _mockRepo.Setup(repo => repo.CreateAccountAsync(It.IsAny<UserAccount>()))
                     .ThrowsAsync(new DuplicateAccountException("El usuario ya existe"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => _accountManager.CreateAccountAsync(newAccountDto)
            );

            Assert.Equal("USER_ALREADY_EXISTS", exception.Detail.ErrorCode);

            // Verificamos que la lógica posterior (generar código, enviar email) nunca se ejecutó.
            _mockCodeService.Verify(service => service.GenerateAndStoreCode(It.IsAny<string>(), It.IsAny<CodeType>()), Times.Never);
            _mockNotification.Verify(service => service.SendAccountVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CreateAccount_ShouldThrowFaultException_WhenDatabaseFails()
        {
            // Arrange
            var newAccountDto = new NewAccountDTO
            {
                Email = "test@example.com",
                Password = "AValidPassword123!" // Dato necesario para que BCrypt no falle
            };

            _mockRepo.Setup(repo => repo.CreateAccountAsync(It.IsAny<UserAccount>()))
                     .ThrowsAsync(new System.Data.Entity.Infrastructure.DbUpdateException("Error de BD"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => _accountManager.CreateAccountAsync(newAccountDto)
            );

            Assert.Equal("DATABASE_ERROR", exception.Detail.ErrorCode);

            // Verificamos que NUNCA se intentó generar un código ni enviar un email.
            _mockCodeService.Verify(service => service.GenerateAndStoreCode(It.IsAny<string>(), It.IsAny<CodeType>()), Times.Never);
            _mockNotification.Verify(service => service.SendAccountVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}
