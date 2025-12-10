using PASSWORD_LIS_Client.Utils;
using Xunit;

namespace Test.UtilsTests
{
    public class ValidationUtilsTests
    {

        [Fact]
        public void ContainsOnlyLetters_WithOnlyLetters_ShouldReturnTrue()
        {
            var text = "abcdefghijklmnopqrstuvwxyz";

            var result = ValidationUtils.ContainsOnlyLetters(text);

            Assert.True(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithUpperCaseLetters_ShouldReturnTrue()
        {
            var text = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            var result = ValidationUtils.ContainsOnlyLetters(text);

            Assert.True(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithMixedCaseLetters_ShouldReturnTrue()
        {
            var text = "AbCdEfGh";

            var result = ValidationUtils.ContainsOnlyLetters(text);

            Assert.True(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithSpaces_ShouldReturnTrue()
        {
            var text = "Hello World";

            var result = ValidationUtils.ContainsOnlyLetters(text);

            Assert.True(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithSpanishCharacters_ShouldReturnTrue()
        {
            var text = "Ò—·ÈÌÛ˙¡…Õ”⁄¸‹";

            var result = ValidationUtils.ContainsOnlyLetters(text);

            Assert.True(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithSpanishName_ShouldReturnTrue()
        {
            var text = "JosÈ MarÌa";

            var result = ValidationUtils.ContainsOnlyLetters(text);

            Assert.True(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithNumbers_ShouldReturnFalse()
        {
            var text = "abc123";

            var result = ValidationUtils.ContainsOnlyLetters(text);

            Assert.False(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithSpecialCharacters_ShouldReturnFalse()
        {
            var text = "Hello@World";

            var result = ValidationUtils.ContainsOnlyLetters(text);

            Assert.False(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithPunctuation_ShouldReturnFalse()
        {
            var text = "Hello, World!";

            var result = ValidationUtils.ContainsOnlyLetters(text);

            Assert.False(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithEmptyString_ShouldReturnTrue()
        {
            var text = string.Empty;

            var result = ValidationUtils.ContainsOnlyLetters(text);

            Assert.True(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithNull_ShouldReturnTrue()
        {
            string text = null;

            var result = ValidationUtils.ContainsOnlyLetters(text);

            Assert.True(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithOnlySpaces_ShouldReturnTrue()
        {
            var text = "   ";

            var result = ValidationUtils.ContainsOnlyLetters(text);

            Assert.True(result);
        }


        [Fact]
        public void ArePasswordRequirementsMet_WithValidPassword_ShouldReturnTrue()
        {
            var password = "Pass123!";

            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            Assert.True(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithAllRequirements_ShouldReturnTrue()
        {
            var password = "MyP@ssw0rd";

            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            Assert.True(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithMinimumLength_ShouldReturnTrue()
        {
            var password = "Abcd12#$";

            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            Assert.True(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithMaximumLength_ShouldReturnTrue()
        {
            var password = "Abcd12#$1234567";

            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            Assert.True(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithNoLowerCase_ShouldReturnFalse()
        {
            var password = "PASS123!";

            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            Assert.False(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithNoUpperCase_ShouldReturnFalse()
        {
            var password = "pass123!";

            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            Assert.False(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithNoNumbers_ShouldReturnFalse()
        {
            var password = "Password!";

            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            Assert.False(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithNoSpecialCharacters_ShouldReturnFalse()
        {
            var password = "Password123";

            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            Assert.False(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithTooShort_ShouldReturnFalse()
        {
            var password = "Pass12!";

            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            Assert.False(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithTooLong_ShouldReturnFalse()
        {
            var password = "Pass12!123456789";

            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            Assert.False(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithEmptyString_ShouldReturnFalse()
        {
            var password = string.Empty;

            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            Assert.False(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithNull_ShouldReturnFalse()
        {
            string password = null;

            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            Assert.False(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithOnlyLetters_ShouldReturnFalse()
        {
            var password = "PasswordOnly";

            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            Assert.False(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithMultipleSpecialChars_ShouldReturnTrue()
        {
            var password = "P@ssw0rd!#";

            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            Assert.True(result);
        }


        [Fact]
        public void IsValidEmail_WithValidEmail_ShouldReturnTrue()
        {
            var email = "test@example.com";

            var result = ValidationUtils.IsValidEmail(email);

            Assert.True(result);
        }

        [Fact]
        public void IsValidEmail_WithValidEmailWithNumbers_ShouldReturnTrue()
        {
            var email = "user123@domain456.com";

            var result = ValidationUtils.IsValidEmail(email);

            Assert.True(result);
        }

        [Fact]
        public void IsValidEmail_WithValidEmailWithDots_ShouldReturnTrue()
        {
            var email = "first.last@company.co.uk";

            var result = ValidationUtils.IsValidEmail(email);

            Assert.True(result);
        }

        [Fact]
        public void IsValidEmail_WithValidEmailWithPlus_ShouldReturnTrue()
        {
            var email = "user+tag@example.com";

            var result = ValidationUtils.IsValidEmail(email);

            Assert.True(result);
        }

        [Fact]
        public void IsValidEmail_WithValidEmailWithUnderscore_ShouldReturnTrue()
        {
            var email = "first_last@example.com";

            var result = ValidationUtils.IsValidEmail(email);

            Assert.True(result);
        }

        [Fact]
        public void IsValidEmail_WithValidEmailWithHyphen_ShouldReturnTrue()
        {
            var email = "user@sub-domain.example.com";

            var result = ValidationUtils.IsValidEmail(email);

            Assert.True(result);
        }

        [Fact]
        public void IsValidEmail_WithEmptyString_ShouldReturnFalse()
        {
            var email = string.Empty;

            var result = ValidationUtils.IsValidEmail(email);

            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_WithNull_ShouldReturnFalse()
        {
            string email = null;

            var result = ValidationUtils.IsValidEmail(email);

            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_WithWhitespace_ShouldReturnFalse()
        {
            var email = "   ";

            var result = ValidationUtils.IsValidEmail(email);

            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_WithNoAtSymbol_ShouldReturnFalse()
        {
            var email = "testexample.com";

            var result = ValidationUtils.IsValidEmail(email);

            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_WithNoDomain_ShouldReturnFalse()
        {
            var email = "test@";

            var result = ValidationUtils.IsValidEmail(email);

            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_WithNoLocalPart_ShouldReturnFalse()
        {
            var email = "@example.com";

            var result = ValidationUtils.IsValidEmail(email);

            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_WithNoTopLevelDomain_ShouldReturnFalse()
        {
            var email = "test@example";

            var result = ValidationUtils.IsValidEmail(email);

            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_WithInvalidCharacters_ShouldReturnFalse()
        {
            var email = "test#user@example.com";

            var result = ValidationUtils.IsValidEmail(email);

            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_WithSpaces_ShouldReturnFalse()
        {
            var email = "test user@example.com";

            var result = ValidationUtils.IsValidEmail(email);

            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_WithMultipleAtSymbols_ShouldReturnFalse()
        {
            var email = "test@@example.com";

            var result = ValidationUtils.IsValidEmail(email);

            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_WithShortTopLevelDomain_ShouldReturnTrue()
        {
            var email = "test@example.co";

            var result = ValidationUtils.IsValidEmail(email);

            Assert.True(result);
        }

        [Fact]
        public void IsValidEmail_WithSingleCharTopLevelDomain_ShouldReturnFalse()
        {
            var email = "test@example.c";

            var result = ValidationUtils.IsValidEmail(email);

            Assert.False(result);
        }

        [Fact]
        public void PasswordsMatch_WithMatchingPasswords_ShouldReturnTrue()
        {
            var password = "Password123!";
            var confirmPassword = "Password123!";

            var result = ValidationUtils.PasswordsMatch(password, confirmPassword);

            Assert.True(result);
        }

        [Fact]
        public void PasswordsMatch_WithDifferentPasswords_ShouldReturnFalse()
        {
            var password = "Password123!";
            var confirmPassword = "DifferentPass456!";

            var result = ValidationUtils.PasswordsMatch(password, confirmPassword);

            Assert.False(result);
        }

        [Fact]
        public void PasswordsMatch_WithCaseDifference_ShouldReturnFalse()
        {
            var password = "Password123!";
            var confirmPassword = "password123!";

            var result = ValidationUtils.PasswordsMatch(password, confirmPassword);

            Assert.False(result);
        }

        [Fact]
        public void PasswordsMatch_WithBothEmpty_ShouldReturnTrue()
        {
            var password = string.Empty;
            var confirmPassword = string.Empty;

            var result = ValidationUtils.PasswordsMatch(password, confirmPassword);

            Assert.True(result);
        }

        [Fact]
        public void PasswordsMatch_WithBothNull_ShouldReturnTrue()
        {
            string password = null;
            string confirmPassword = null;

            var result = ValidationUtils.PasswordsMatch(password, confirmPassword);

            Assert.True(result);
        }

        [Fact]
        public void PasswordsMatch_WithOneNull_ShouldReturnFalse()
        {
            var password = "Password123!";
            string confirmPassword = null;

            var result = ValidationUtils.PasswordsMatch(password, confirmPassword);

            Assert.False(result);
        }

        [Fact]
        public void PasswordsMatch_WithOneEmpty_ShouldReturnFalse()
        {
            var password = "Password123!";
            var confirmPassword = string.Empty;

            var result = ValidationUtils.PasswordsMatch(password, confirmPassword);

            Assert.False(result);
        }

        [Fact]
        public void PasswordsMatch_WithWhitespaceInOne_ShouldReturnFalse()
        {
            var password = "Password123!";
            var confirmPassword = "Password123! ";

            var result = ValidationUtils.PasswordsMatch(password, confirmPassword);

            Assert.False(result);
        }

        [Fact]
        public void PasswordsMatch_WithIdenticalWhitespace_ShouldReturnTrue()
        {
            var password = "Pass word123!";
            var confirmPassword = "Pass word123!";

            var result = ValidationUtils.PasswordsMatch(password, confirmPassword);

            Assert.True(result);
        }

    }
}
