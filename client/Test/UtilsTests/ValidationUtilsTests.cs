using PASSWORD_LIS_Client.Utils;
using Xunit;

namespace Test.UtilsTests
{
    public class ValidationUtilsTests
    {
        #region ContainsOnlyLetters Tests

        [Fact]
        public void ContainsOnlyLetters_WithOnlyLetters_ShouldReturnTrue()
        {
            // Arrange
            var text = "abcdefghijklmnopqrstuvwxyz";

            // Act
            var result = ValidationUtils.ContainsOnlyLetters(text);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithUpperCaseLetters_ShouldReturnTrue()
        {
            // Arrange
            var text = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            // Act
            var result = ValidationUtils.ContainsOnlyLetters(text);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithMixedCaseLetters_ShouldReturnTrue()
        {
            // Arrange
            var text = "AbCdEfGh";

            // Act
            var result = ValidationUtils.ContainsOnlyLetters(text);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithSpaces_ShouldReturnTrue()
        {
            // Arrange
            var text = "Hello World";

            // Act
            var result = ValidationUtils.ContainsOnlyLetters(text);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithSpanishCharacters_ShouldReturnTrue()
        {
            // Arrange
            var text = "Ò—·ÈÌÛ˙¡…Õ”⁄¸‹";

            // Act
            var result = ValidationUtils.ContainsOnlyLetters(text);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithSpanishName_ShouldReturnTrue()
        {
            // Arrange
            var text = "JosÈ MarÌa";

            // Act
            var result = ValidationUtils.ContainsOnlyLetters(text);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithNumbers_ShouldReturnFalse()
        {
            // Arrange
            var text = "abc123";

            // Act
            var result = ValidationUtils.ContainsOnlyLetters(text);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithSpecialCharacters_ShouldReturnFalse()
        {
            // Arrange
            var text = "Hello@World";

            // Act
            var result = ValidationUtils.ContainsOnlyLetters(text);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithPunctuation_ShouldReturnFalse()
        {
            // Arrange
            var text = "Hello, World!";

            // Act
            var result = ValidationUtils.ContainsOnlyLetters(text);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithEmptyString_ShouldReturnTrue()
        {
            // Arrange
            var text = string.Empty;

            // Act
            var result = ValidationUtils.ContainsOnlyLetters(text);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithNull_ShouldReturnTrue()
        {
            // Arrange
            string text = null;

            // Act
            var result = ValidationUtils.ContainsOnlyLetters(text);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ContainsOnlyLetters_WithOnlySpaces_ShouldReturnTrue()
        {
            // Arrange
            var text = "   ";

            // Act
            var result = ValidationUtils.ContainsOnlyLetters(text);

            // Assert
            Assert.True(result);
        }

        #endregion

        #region ArePasswordRequirementsMet Tests

        [Fact]
        public void ArePasswordRequirementsMet_WithValidPassword_ShouldReturnTrue()
        {
            // Arrange
            var password = "Pass123!";

            // Act
            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithAllRequirements_ShouldReturnTrue()
        {
            // Arrange
            var password = "MyP@ssw0rd";

            // Act
            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithMinimumLength_ShouldReturnTrue()
        {
            // Arrange - 8 characters exactly
            var password = "Abcd12#$";

            // Act
            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithMaximumLength_ShouldReturnTrue()
        {
            // Arrange - 15 characters exactly
            var password = "Abcd12#$1234567";

            // Act
            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithNoLowerCase_ShouldReturnFalse()
        {
            // Arrange
            var password = "PASS123!";

            // Act
            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithNoUpperCase_ShouldReturnFalse()
        {
            // Arrange
            var password = "pass123!";

            // Act
            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithNoNumbers_ShouldReturnFalse()
        {
            // Arrange
            var password = "Password!";

            // Act
            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithNoSpecialCharacters_ShouldReturnFalse()
        {
            // Arrange
            var password = "Password123";

            // Act
            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithTooShort_ShouldReturnFalse()
        {
            // Arrange - 7 characters
            var password = "Pass12!";

            // Act
            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithTooLong_ShouldReturnFalse()
        {
            // Arrange - 16 characters
            var password = "Pass12!123456789";

            // Act
            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithEmptyString_ShouldReturnFalse()
        {
            // Arrange
            var password = string.Empty;

            // Act
            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithNull_ShouldReturnFalse()
        {
            // Arrange
            string password = null;

            // Act
            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithOnlyLetters_ShouldReturnFalse()
        {
            // Arrange
            var password = "PasswordOnly";

            // Act
            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ArePasswordRequirementsMet_WithMultipleSpecialChars_ShouldReturnTrue()
        {
            // Arrange
            var password = "P@ssw0rd!#";

            // Act
            var result = ValidationUtils.ArePasswordRequirementsMet(password);

            // Assert
            Assert.True(result);
        }

        #endregion

        #region IsValidEmail Tests

        [Fact]
        public void IsValidEmail_WithValidEmail_ShouldReturnTrue()
        {
            // Arrange
            var email = "test@example.com";

            // Act
            var result = ValidationUtils.IsValidEmail(email);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsValidEmail_WithValidEmailWithNumbers_ShouldReturnTrue()
        {
            // Arrange
            var email = "user123@domain456.com";

            // Act
            var result = ValidationUtils.IsValidEmail(email);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsValidEmail_WithValidEmailWithDots_ShouldReturnTrue()
        {
            // Arrange
            var email = "first.last@company.co.uk";

            // Act
            var result = ValidationUtils.IsValidEmail(email);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsValidEmail_WithValidEmailWithPlus_ShouldReturnTrue()
        {
            // Arrange
            var email = "user+tag@example.com";

            // Act
            var result = ValidationUtils.IsValidEmail(email);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsValidEmail_WithValidEmailWithUnderscore_ShouldReturnTrue()
        {
            // Arrange
            var email = "first_last@example.com";

            // Act
            var result = ValidationUtils.IsValidEmail(email);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsValidEmail_WithValidEmailWithHyphen_ShouldReturnTrue()
        {
            // Arrange
            var email = "user@sub-domain.example.com";

            // Act
            var result = ValidationUtils.IsValidEmail(email);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsValidEmail_WithEmptyString_ShouldReturnFalse()
        {
            // Arrange
            var email = string.Empty;

            // Act
            var result = ValidationUtils.IsValidEmail(email);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_WithNull_ShouldReturnFalse()
        {
            // Arrange
            string email = null;

            // Act
            var result = ValidationUtils.IsValidEmail(email);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_WithWhitespace_ShouldReturnFalse()
        {
            // Arrange
            var email = "   ";

            // Act
            var result = ValidationUtils.IsValidEmail(email);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_WithNoAtSymbol_ShouldReturnFalse()
        {
            // Arrange
            var email = "testexample.com";

            // Act
            var result = ValidationUtils.IsValidEmail(email);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_WithNoDomain_ShouldReturnFalse()
        {
            // Arrange
            var email = "test@";

            // Act
            var result = ValidationUtils.IsValidEmail(email);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_WithNoLocalPart_ShouldReturnFalse()
        {
            // Arrange
            var email = "@example.com";

            // Act
            var result = ValidationUtils.IsValidEmail(email);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_WithNoTopLevelDomain_ShouldReturnFalse()
        {
            // Arrange
            var email = "test@example";

            // Act
            var result = ValidationUtils.IsValidEmail(email);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_WithInvalidCharacters_ShouldReturnFalse()
        {
            // Arrange
            var email = "test#user@example.com";

            // Act
            var result = ValidationUtils.IsValidEmail(email);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_WithSpaces_ShouldReturnFalse()
        {
            // Arrange
            var email = "test user@example.com";

            // Act
            var result = ValidationUtils.IsValidEmail(email);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_WithMultipleAtSymbols_ShouldReturnFalse()
        {
            // Arrange
            var email = "test@@example.com";

            // Act
            var result = ValidationUtils.IsValidEmail(email);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsValidEmail_WithShortTopLevelDomain_ShouldReturnTrue()
        {
            // Arrange
            var email = "test@example.co";

            // Act
            var result = ValidationUtils.IsValidEmail(email);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsValidEmail_WithSingleCharTopLevelDomain_ShouldReturnFalse()
        {
            // Arrange
            var email = "test@example.c";

            // Act
            var result = ValidationUtils.IsValidEmail(email);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region PasswordsMatch Tests

        [Fact]
        public void PasswordsMatch_WithMatchingPasswords_ShouldReturnTrue()
        {
            // Arrange
            var password = "Password123!";
            var confirmPassword = "Password123!";

            // Act
            var result = ValidationUtils.PasswordsMatch(password, confirmPassword);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void PasswordsMatch_WithDifferentPasswords_ShouldReturnFalse()
        {
            // Arrange
            var password = "Password123!";
            var confirmPassword = "DifferentPass456!";

            // Act
            var result = ValidationUtils.PasswordsMatch(password, confirmPassword);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void PasswordsMatch_WithCaseDifference_ShouldReturnFalse()
        {
            // Arrange
            var password = "Password123!";
            var confirmPassword = "password123!";

            // Act
            var result = ValidationUtils.PasswordsMatch(password, confirmPassword);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void PasswordsMatch_WithBothEmpty_ShouldReturnTrue()
        {
            // Arrange
            var password = string.Empty;
            var confirmPassword = string.Empty;

            // Act
            var result = ValidationUtils.PasswordsMatch(password, confirmPassword);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void PasswordsMatch_WithBothNull_ShouldReturnTrue()
        {
            // Arrange
            string password = null;
            string confirmPassword = null;

            // Act
            var result = ValidationUtils.PasswordsMatch(password, confirmPassword);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void PasswordsMatch_WithOneNull_ShouldReturnFalse()
        {
            // Arrange
            var password = "Password123!";
            string confirmPassword = null;

            // Act
            var result = ValidationUtils.PasswordsMatch(password, confirmPassword);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void PasswordsMatch_WithOneEmpty_ShouldReturnFalse()
        {
            // Arrange
            var password = "Password123!";
            var confirmPassword = string.Empty;

            // Act
            var result = ValidationUtils.PasswordsMatch(password, confirmPassword);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void PasswordsMatch_WithWhitespaceInOne_ShouldReturnFalse()
        {
            // Arrange
            var password = "Password123!";
            var confirmPassword = "Password123! ";

            // Act
            var result = ValidationUtils.PasswordsMatch(password, confirmPassword);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void PasswordsMatch_WithIdenticalWhitespace_ShouldReturnTrue()
        {
            // Arrange
            var password = "Pass word123!";
            var confirmPassword = "Pass word123!";

            // Act
            var result = ValidationUtils.PasswordsMatch(password, confirmPassword);

            // Assert
            Assert.True(result);
        }

        #endregion
    }
}
