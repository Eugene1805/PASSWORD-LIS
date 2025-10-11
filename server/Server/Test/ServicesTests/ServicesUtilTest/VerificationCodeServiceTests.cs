using Services.Util;

namespace Test.ServicesTests.ServicesUtilTest
{
    public class VerificationCodeServiceTests
    {
        [Fact]
        public void GenerateAndStoreCode_ShouldReturn_6DigitString()
        {
            // Arrange
            var codeService = new VerificationCodeService();
            var email = "test@example.com";

            // Act
            var code = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);

            // Assert
            Assert.NotNull(code);
            Assert.Equal(6, code.Length);
            Assert.True(int.TryParse(code, out _));
        }

        [Fact]
        public void ValidateCode_ShouldReturnTrue_ForValidAndUnexpiredCode()
        {
            // Arrange
            var codeService = new VerificationCodeService();
            var email = "test@example.com";
            var code = codeService.GenerateAndStoreCode(email, CodeType.PasswordReset);

            // Act
            var isValid = codeService.ValidateCode(email, code, CodeType.PasswordReset);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void ValidateCode_ShouldReturnFalse_ForInvalidCode()
        {
            // Arrange
            var codeService = new VerificationCodeService();
            var email = "test@example.com";
            codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);

            // Act
            var isValid = codeService.ValidateCode(email, "000000", CodeType.EmailVerification);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void ValidateCode_ShouldReturnFalse_AfterCodeIsUsedOnce()
        {
            // Arrange
            var codeService = new VerificationCodeService();
            var email = "test@example.com";
            var code = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);

            // Act
            var firstAttempt = codeService.ValidateCode(email, code, CodeType.EmailVerification);
            var secondAttempt = codeService.ValidateCode(email, code, CodeType.EmailVerification);

            // Assert
            Assert.True(firstAttempt); // La primera vez debe ser válido.
            Assert.False(secondAttempt); // La segunda vez ya no debe existir.
        }

        [Fact]
        public void ValidateCode_ShouldReturnFalse_ForCorrectCodeButWrongType()
        {
            // Arrange
            var codeService = new VerificationCodeService();
            var email = "test@example.com";
            var code = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);

            // Act
            // Intentamos validar un código de verificación de email como si fuera de reseteo de password.
            var isValid = codeService.ValidateCode(email, code, CodeType.PasswordReset);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void CanRequestCode_ShouldReturnFalse_ImmediatelyAfterGeneratingCode()
        {
            // Arrange
            var codeService = new VerificationCodeService();
            var email = "test@example.com";
            codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);

            // Act
            var canRequest = codeService.CanRequestCode(email, CodeType.EmailVerification);

            // Assert
            Assert.False(canRequest);
        }

        [Fact]
        public void DifferentCodeTypes_ShouldNotConflict_ForSameUser()
        {
            // Arrange
            var codeService = new VerificationCodeService();
            var email = "user@example.com";

            // Act
            var verificationCode = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);
            var resetCode = codeService.GenerateAndStoreCode(email, CodeType.PasswordReset);

            // Assert
            // Verificamos que ambos códigos se puedan validar correctamente, demostrando que no se sobrescribieron.
            Assert.True(codeService.ValidateCode(email, verificationCode, CodeType.EmailVerification));
            Assert.True(codeService.ValidateCode(email, resetCode, CodeType.PasswordReset));
        }
    
    }
}
