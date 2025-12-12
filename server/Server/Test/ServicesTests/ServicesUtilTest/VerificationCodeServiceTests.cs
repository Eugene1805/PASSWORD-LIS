using Services.Util;

namespace Test.ServicesTests.ServicesUtilTest
{
    public class VerificationCodeServiceTests
    {
        [Fact]
        public void GenerateAndStoreCode_ShouldReturn_6DigitString()
        {

            VerificationCodeService codeService = new VerificationCodeService();
            string email = "test@example.com";


            string code = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);


            var expected = new 
            { 
                NotNull = true, Length = 6,
                IsNumeric = true 
            };
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

            VerificationCodeService codeService = new VerificationCodeService();
            string email = "test@example.com";
            string code = codeService.GenerateAndStoreCode(email, CodeType.PasswordReset);


            bool isValid = codeService.ValidateCode(email, code, CodeType.PasswordReset);


            Assert.True(isValid);
        }

        [Fact]
        public void ValidateCode_ShouldReturnFalse_ForInvalidCode()
        {

            VerificationCodeService codeService = new VerificationCodeService();
            string email = "test@example.com";
            _ = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);


            bool isValid = codeService.ValidateCode(email, "000000", CodeType.EmailVerification);


            Assert.False(isValid);
        }

        [Fact]
        public void ValidateCode_ShouldReturnFalse_AfterCodeIsUsedOnce()
        {

            VerificationCodeService codeService = new VerificationCodeService();
            string email = "test@example.com";
            string code = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);


            bool firstAttempt = codeService.ValidateCode(email, code, CodeType.EmailVerification);
            bool secondAttempt = codeService.ValidateCode(email, code, CodeType.EmailVerification);


            Assert.Equal((true, false), (firstAttempt, secondAttempt));
        }

        [Fact]
        public void ValidateCode_ShouldReturnFalse_ForCorrectCodeButWrongType()
        {

            VerificationCodeService codeService = new VerificationCodeService();
            string email = "test@example.com";
            string code = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);


            bool isValid = codeService.ValidateCode(email, code, CodeType.PasswordReset);


            Assert.False(isValid);
        }

        [Fact]
        public void CanRequestCode_ShouldReturnFalse_ImmediatelyAfterGeneratingCode()
        {

            VerificationCodeService codeService = new VerificationCodeService();
            string email = "test@example.com";
            _ = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);


            bool canRequest = codeService.CanRequestCode(email, CodeType.EmailVerification);


            Assert.False(canRequest);
        }

        [Fact]
        public void DifferentCodeTypes_ShouldNotConflict_ForSameUser()
        {

            VerificationCodeService codeService = new VerificationCodeService();
            string email = "user@example.com";


            string verificationCode = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);
            string resetCode = codeService.GenerateAndStoreCode(email, CodeType.PasswordReset);
            bool emailValid = codeService.ValidateCode(email, verificationCode, CodeType.EmailVerification);
            bool resetValid = codeService.ValidateCode(email, resetCode, CodeType.PasswordReset);


            Assert.Equal((true, true), (emailValid, resetValid));
        }
    }
}
