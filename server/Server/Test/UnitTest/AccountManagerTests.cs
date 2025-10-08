using Data.DAL.Interfaces;
using Data.Model;
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
    public class AccountManagerTests
    {
        [Fact]
        public void CreateAccount_WhenRepositorySucceeds_ShouldSendEmailAndReturnTrue()
        {
            // Arrange (Preparar)
            // 1. Crear "Mocks" (simuladores) de las dependencias
            var mockRepo = new Mock<IAccountRepository>();
            var mockEmailSender = new Mock<IEmailSender>();

            var newAccountDto = new NewAccountDTO { Email = "test@example.com", /* ... */ };

            // 2. Configurar el comportamiento de los mocks
            // "Cuando se llame a CreateAccount, simula que funciona y devuelve true"
            mockRepo.Setup(r => r.CreateAccount(It.IsAny<UserAccount>())).Returns(true);

            // 3. Crear la instancia de la clase que queremos probar, pasándole los mocks
            var accountManager = new AccountManager(mockRepo.Object, mockEmailSender.Object);

            // Act (Actuar)
            var result = accountManager.CreateAccount(newAccountDto);

            // Assert (Verificar)
            Assert.True(result);

            // Verificar que el método de enviar email fue llamado exactamente una vez
            mockEmailSender.Verify(s => s.SendVerificationEmailAsync(newAccountDto.Email, It.IsAny<string>()), Times.Once);
        }
    }
}
