using Services.Util;

namespace Test.ServicesTests.ServicesUtilTest
{
    public class VerificationCodeServiceTests
    {
        [Fact]
        public void GenerateAndStoreCode_ShouldReturn_6DigitString()
        {
            // Arrange
            VerificationCodeService codeService = new VerificationCodeService();
            string email = "test@example.com";

            // Act
            string code = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);

            // Assert
            var expected = new { NotNull = true, Length = 6, IsNumeric = true };
            var actual = new
            {
                NotNull = code != null,
                Length = code?.Length ?? 0,
                IsNumeric = code != null && int.TryParse(code, out _)
            };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ValidateCode_ShouldReturnTrue_ForValidAndUnexpiredCode()
        {
            // Arrange
            VerificationCodeService codeService = new VerificationCodeService();
            string email = "test@example.com";
            string code = codeService.GenerateAndStoreCode(email, CodeType.PasswordReset);

            // Act
            bool isValid = codeService.ValidateCode(email, code, CodeType.PasswordReset);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void ValidateCode_ShouldReturnFalse_ForInvalidCode()
        {
            // Arrange
            VerificationCodeService codeService = new VerificationCodeService();
            string email = "test@example.com";
            _ = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);

            // Act
            bool isValid = codeService.ValidateCode(email, "000000", CodeType.EmailVerification);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void ValidateCode_ShouldReturnFalse_AfterCodeIsUsedOnce()
        {
            // Arrange
            VerificationCodeService codeService = new VerificationCodeService();
            string email = "test@example.com";
            string code = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);

            // Act
            bool firstAttempt = codeService.ValidateCode(email, code, CodeType.EmailVerification);
            bool secondAttempt = codeService.ValidateCode(email, code, CodeType.EmailVerification);

            // Assert
            Assert.Equal((true, false), (firstAttempt, secondAttempt));
        }

        [Fact]
        public void ValidateCode_ShouldReturnFalse_ForCorrectCodeButWrongType()
        {
            // Arrange
            VerificationCodeService codeService = new VerificationCodeService();
            string email = "test@example.com";
            string code = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);

            // Act
            bool isValid = codeService.ValidateCode(email, code, CodeType.PasswordReset);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void CanRequestCode_ShouldReturnFalse_ImmediatelyAfterGeneratingCode()
        {
            // Arrange
            VerificationCodeService codeService = new VerificationCodeService();
            string email = "test@example.com";
            _ = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);

            // Act
            bool canRequest = codeService.CanRequestCode(email, CodeType.EmailVerification);

            // Assert
            Assert.False(canRequest);
        }

        [Fact]
        public void DifferentCodeTypes_ShouldNotConflict_ForSameUser()
        {
            // Arrange
            VerificationCodeService codeService = new VerificationCodeService();
            string email = "user@example.com";

            // Act
            string verificationCode = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);
            string resetCode = codeService.GenerateAndStoreCode(email, CodeType.PasswordReset);
            bool emailValid = codeService.ValidateCode(email, verificationCode, CodeType.EmailVerification);
            bool resetValid = codeService.ValidateCode(email, resetCode, CodeType.PasswordReset);

            // Assert
            Assert.Equal((true, true), (emailValid, resetValid));
        }
    }
}
