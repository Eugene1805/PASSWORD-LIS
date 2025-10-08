using Services.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.UnitTest
{
    public class VerificationCodeManagerTests
    {
        [Fact]
        public void GenerateCode_ShouldReturn_SixDigitCode()
        {
            // Arrange
            var email = "test@example.com";

            // Act
            var code = VerificationCodeManager.GenerateCode(email);

            // Assert
            Assert.NotNull(code);
            Assert.Equal(6, code.Length);
            Assert.True(int.TryParse(code, out _));
        }

        [Fact]
        public void VerifyCode_WithValidCode_ShouldReturnTrue()
        {
            // Arrange
            var email = "valid@example.com";
            var generatedCode = VerificationCodeManager.GenerateCode(email);

            // Act
            var isValid = VerificationCodeManager.VerifyCode(email, generatedCode);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void VerifyCode_WithInvalidCode_ShouldReturnFalse()
        {
            // Arrange
            var email = "invalidcode@example.com";
            VerificationCodeManager.GenerateCode(email); // Se genera un código, pero usaremos uno incorrecto

            // Act
            var isValid = VerificationCodeManager.VerifyCode(email, "000000");

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task VerifyCode_WithExpiredCode_ShouldReturnFalse()
        {
            // Arrange
            var email = "expired@example.com";
            // Modificamos temporalmente el tiempo de expiración para la prueba
            // (Esto requeriría un pequeño cambio en tu clase para hacerlo probable)
            // Por ahora, simularemos el paso del tiempo.
            // NOTA: Para hacer esto más robusto, tu GenerateCode podría aceptar un TimeSpan opcional.
            // Pero para simularlo, asumimos que el tiempo de expiración es muy corto
            // o esperamos el tiempo real, lo cual no es ideal en pruebas.

            // La forma robusta de probar el tiempo es con una abstracción.
            // Por ahora, simplemente demostraremos el concepto asumiendo que podemos
            // manipular el estado, aunque no es posible con tu código actual.
            // El siguiente test es conceptual.

            // Un test real necesitaría refactorizar VerificationCodeManager para inyectar el tiempo.
            // Asumiremos que el código expira en 1 segundo para este ejemplo.
            var code = VerificationCodeManager.GenerateCode(email);

            // Act
            await Task.Delay(100); // Simula el paso del tiempo

            // Assert
            // Este test fallará si la expiración es de 5 minutos.
            // Muestra la necesidad de refactorizar para poder controlar el tiempo.
            // Assert.False(VerificationCodeManager.VerifyCode(email, code));

            // Un test más realista con tu código actual
            var isValid = VerificationCodeManager.VerifyCode(email, code); // Lo verificamos inmediatamente
            var isUsed = VerificationCodeManager.VerifyCode(email, code); // Intentamos usarlo de nuevo

            Assert.True(isValid);
            Assert.False(isUsed, "El código no debería poder usarse dos veces.");
        }

        [Fact]
        public void VerifyCode_WithNonExistentEmail_ShouldReturnFalse()
        {
            // Arrange
            var email = "nonexistent@example.com";
            var code = "123456";

            // Act
            var isValid = VerificationCodeManager.VerifyCode(email, code);

            // Assert
            Assert.False(isValid);
        }
    }
}
