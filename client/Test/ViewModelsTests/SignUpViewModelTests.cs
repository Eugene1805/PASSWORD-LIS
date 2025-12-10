using Moq;
using PASSWORD_LIS_Client.AccountManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.ViewModels;
using PASSWORD_LIS_Client.Views;
using System.ServiceModel;
using System.Threading.Tasks;
using Xunit;

namespace Test.ViewModelsTests
{
    public class SignUpViewModelTests
    {
        private readonly Mock<IAccountManagerService> mockAccountService;
        private readonly Mock<IWindowService> mockWindowService;
        private readonly SignUpViewModel viewModel;

        public SignUpViewModelTests()
        {
            mockAccountService = new Mock<IAccountManagerService>();
            mockWindowService = new Mock<IWindowService>();
            viewModel = new SignUpViewModel(mockAccountService.Object, mockWindowService.Object);
        }

        [Fact]
        public async Task SignUpCommand_WhenInputIsValidAndServiceSucceeds_ShouldNavigateToVerifyCode()
        {
            // Arrange
            viewModel.FirstName = "Test";
            viewModel.LastName = "User";
            viewModel.Nickname = "tester";
            viewModel.Email = "test@example.com";
            viewModel.Password = "ValidPass1!";
            viewModel.ConfirmPassword = "ValidPass1!";

            mockAccountService.Setup(s => s.IsNicknameInUseAsync(It.IsAny<string>()))
                               .ReturnsAsync(false);

            mockAccountService.Setup(s => s.CreateAccountAsync(It.IsAny<NewAccountDTO>()))
                               .Returns(Task.CompletedTask);

            // Act
            await viewModel.SignUpAsync();

            // Assert
            mockAccountService.Verify(s => s.IsNicknameInUseAsync(It.IsAny<string>()), Times.Once);
            mockAccountService.Verify(s => s.CreateAccountAsync(It.IsAny<NewAccountDTO>()), Times.Once);
            mockWindowService.Verify(w => w.ShowVerifyCodeWindow(viewModel.Email, It.IsAny<VerificationReason>()), 
                Times.Once);
            mockWindowService.Verify(w => w.CloseWindow(viewModel), Times.Once);
        }

        [Fact]
        public async Task SignUpCommand_WhenServiceThrowsFaultException_ShouldShowUserExistsPopUp()
        {
            // Arrange
            viewModel.FirstName = "Test";
            viewModel.LastName = "User";
            viewModel.Nickname = "tester";
            viewModel.Email = "test@example.com";
            viewModel.Password = "ValidPass1!";
            viewModel.ConfirmPassword = "ValidPass1!";

            mockAccountService.Setup(s => s.IsNicknameInUseAsync(It.IsAny<string>()))
                               .ReturnsAsync(false);

            var errorDetail = new ServiceErrorDetailDTO
            {
                ErrorCode = "USER_ALREADY_EXISTS"
            };
            mockAccountService.Setup(s => s.CreateAccountAsync(It.IsAny<NewAccountDTO>()))
                               .ThrowsAsync(new FaultException<ServiceErrorDetailDTO>(errorDetail));

            // Act
            await viewModel.SignUpAsync();

            // Assert
            mockWindowService.Verify(w => w.ShowPopUp(
                It.Is<string>(s => s == PASSWORD_LIS_Client.Properties.Langs.Lang.errorTitleText),
                It.Is<string>(s => s == PASSWORD_LIS_Client.Properties.Langs.Lang.userAlreadyExistText),
                PopUpIcon.Warning), Times.Once);
            mockWindowService.Verify(w => w.ShowVerifyCodeWindow(It.IsAny<string>(), It.IsAny<VerificationReason>()), 
                Times.Never);
        }

        [Fact]
        public void CanExecuteSignUp_WhenFieldsAreEmpty_ShouldBeFalse()
        {
            // Arrange
            viewModel.Email = ""; 

            // Act
            var canExecute = viewModel.SignUpCommand.CanExecute(null);

            // Assert
            Assert.False(canExecute);
        }

        [Fact]
        public void NavigateToLoginCommand_ShouldShowLoginAndCloseCurrentWindow()
        {
            // Arrange & Act
            viewModel.NavigateToLoginCommand.Execute(null);

            // Assert
            mockWindowService.Verify(w => w.ShowLoginWindow(), Times.Once);
            mockWindowService.Verify(w => w.CloseWindow(viewModel), Times.Once);
        }

        [Theory]
        [InlineData("", "emptyFirstNameText")]
        [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "firstNameTooLongText")]
        [InlineData("Test123", "nameInvalidCharsText")]
        public void FirstName_WhenInvalid_ShouldSetErrorMessage(string value, string errorKey)
        {
            // Act
            viewModel.FirstName = value;

            // Assert
            var expectedError = typeof(PASSWORD_LIS_Client.Properties.Langs.Lang)
                .GetProperty(errorKey)
                .GetValue(null) as string;
            Assert.Equal(expectedError, viewModel.FirstNameError);
        }

        [Fact]
        public void FirstName_WhenValid_ShouldClearErrorMessage()
        {
            // Act
            viewModel.FirstName = "ValidName";

            // Assert
            Assert.Null(viewModel.FirstNameError);
        }

        [Theory]
        [InlineData("", "emptyLastNameText")]
        [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "lastNameTooLongText")]
        [InlineData("Test123", "lastNameInvalidCharsText")]
        public void LastName_WhenInvalid_ShouldSetErrorMessage(string value, string errorKey)
        {
            // Act
            viewModel.LastName = value;

            // Assert
            var expectedError = typeof(PASSWORD_LIS_Client.Properties.Langs.Lang)
                .GetProperty(errorKey)
                .GetValue(null) as string;
            Assert.Equal(expectedError, viewModel.LastNameError);
        }

        [Fact]
        public void LastName_WhenValid_ShouldClearErrorMessage()
        {
            // Act
            viewModel.LastName = "ValidLastName";

            // Assert
            Assert.Null(viewModel.LastNameError);
        }

        [Theory]
        [InlineData("", "emptyNicknameText")]
        [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "nicknameTooLongText")]
        public void Nickname_WhenInvalid_ShouldSetErrorMessage(string value, string errorKey)
        {
            // Act
            viewModel.Nickname = value;

            // Assert
            var expectedError = typeof(PASSWORD_LIS_Client.Properties.Langs.Lang)
                .GetProperty(errorKey)
                .GetValue(null) as string;
            Assert.Equal(expectedError, viewModel.NicknameError);
        }

        [Fact]
        public void Nickname_WhenValid_ShouldClearErrorMessage()
        {
            // Act
            viewModel.Nickname = "ValidNickname";

            // Assert
            Assert.Null(viewModel.NicknameError);
        }

        [Fact]
        public async Task SignUpAsync_WhenNicknameInUse_ShouldSetNicknameError()
        {
            // Arrange
            viewModel.FirstName = "Test";
            viewModel.LastName = "User";
            viewModel.Nickname = "existingNickname";
            viewModel.Email = "test@example.com";
            viewModel.Password = "ValidPass1!";
            viewModel.ConfirmPassword = "ValidPass1!";

            mockAccountService.Setup(s => s.IsNicknameInUseAsync(It.IsAny<string>()))
                               .ReturnsAsync(true);

            // Act
            await viewModel.SignUpAsync();

            // Assert
            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.nicknameInUseText, viewModel.NicknameError);
            mockAccountService.Verify(s => s.CreateAccountAsync(It.IsAny<NewAccountDTO>()), Times.Never);
        }

        [Theory]
        [InlineData("")]
        [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        public void Email_WhenEmptyOrTooLong_ShouldSetErrorMessage(string value)
        {
            // Act
            viewModel.Email = value;

            // Assert
            Assert.NotNull(viewModel.EmailError);
        }

        [Fact]
        public void Email_WhenInvalidFormat_ShouldSetErrorMessage()
        {
            // Act
            viewModel.Email = "invalidemail";

            // Assert
            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.invalidEmailFormatText, viewModel.EmailError);
        }

        [Fact]
        public void Email_WhenValid_ShouldClearErrorMessage()
        {
            // Act
            viewModel.Email = "valid@email.com";

            // Assert
            Assert.Null(viewModel.EmailError);
        }

        [Theory]
        [InlineData("")]
        [InlineData("weak")]
        public void Password_WhenInvalid_ShouldSetErrorMessage(string value)
        {
            // Act
            viewModel.Password = value;

            // Assert
            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.userPasswordRequirementsText, viewModel.PasswordError);
        }

        [Fact]
        public void Password_WhenValid_ShouldClearErrorMessage()
        {
            // Act
            viewModel.Password = "ValidPass1!";

            // Assert
            Assert.Null(viewModel.PasswordError);
        }

        [Theory]
        [InlineData("")]
        [InlineData("DifferentPass1!")]
        public void ConfirmPassword_WhenInvalid_ShouldSetErrorMessage(string confirmPassword)
        {
            // Arrange
            viewModel.Password = "ValidPass1!";

            // Act
            viewModel.ConfirmPassword = confirmPassword;

            // Assert
            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.matchingPasswordErrorText, viewModel.ConfirmPasswordError);
        }

        [Fact]
        public void ConfirmPassword_WhenMatchesPassword_ShouldClearErrorMessage()
        {
            // Arrange
            viewModel.Password = "ValidPass1!";
            viewModel.ConfirmPassword = "ValidPass1!";

            // Assert
            Assert.Null(viewModel.ConfirmPasswordError);
        }

        [Fact]
        public void Password_WhenChangedAfterConfirmPassword_ShouldRevalidateConfirmPassword()
        {
            // Arrange
            viewModel.Password = "ValidPass1!";
            viewModel.ConfirmPassword = "ValidPass1!";
            Assert.Null(viewModel.ConfirmPasswordError);

            // Act
            viewModel.Password = "NewValidPass1!";

            // Assert
            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.matchingPasswordErrorText, viewModel.ConfirmPasswordError);
        }

        [Fact]
        public void CanExecuteSignUp_WhenAllFieldsValidAndFilled_ShouldReturnTrue()
        {
            // Arrange
            viewModel.FirstName = "Test";
            viewModel.LastName = "User";
            viewModel.Nickname = "tester";
            viewModel.Email = "test@example.com";
            viewModel.Password = "ValidPass1!";
            viewModel.ConfirmPassword = "ValidPass1!";

            // Act
            var canExecute = viewModel.SignUpCommand.CanExecute(null);

            // Assert
            Assert.True(canExecute);
        }

        [Fact]
        public void CanExecuteSignUp_WhenAnyFieldHasError_ShouldReturnFalse()
        {
            // Arrange
            viewModel.FirstName = "Test123"; // Invalid
            viewModel.LastName = "User";
            viewModel.Nickname = "tester";
            viewModel.Email = "test@example.com";
            viewModel.Password = "ValidPass1!";
            viewModel.ConfirmPassword = "ValidPass1!";

            // Act
            var canExecute = viewModel.SignUpCommand.CanExecute(null);

            // Assert
            Assert.False(canExecute);
        }

        [Fact]
        public void CanExecuteSignUp_WhenIsSigningUp_ShouldReturnFalse()
        {
            // Arrange
            viewModel.FirstName = "Test";
            viewModel.LastName = "User";
            viewModel.Nickname = "tester";
            viewModel.Email = "test@example.com";
            viewModel.Password = "ValidPass1!";
            viewModel.ConfirmPassword = "ValidPass1!";

            // Simulate signing up
            typeof(SignUpViewModel).GetProperty(nameof(viewModel.IsSigningUp))
                .SetValue(viewModel, true);

            // Act
            var canExecute = viewModel.SignUpCommand.CanExecute(null);

            // Assert
            Assert.False(canExecute);
        }

        [Fact]
        public async Task SignUpAsync_WhenAllFieldsValid_ShouldNotShowAnyPopUp()
        {
            // Arrange
            viewModel.FirstName = "Test";
            viewModel.LastName = "User";
            viewModel.Nickname = "tester";
            viewModel.Email = "test@example.com";
            viewModel.Password = "ValidPass1!";
            viewModel.ConfirmPassword = "ValidPass1!";

            mockAccountService.Setup(s => s.IsNicknameInUseAsync(It.IsAny<string>()))
                               .ReturnsAsync(false);
            mockAccountService.Setup(s => s.CreateAccountAsync(It.IsAny<NewAccountDTO>()))
                               .Returns(Task.CompletedTask);

            // Act
            await viewModel.SignUpAsync();

            // Assert
            Assert.Null(viewModel.FirstNameError);
            Assert.Null(viewModel.LastNameError);
            Assert.Null(viewModel.NicknameError);
            Assert.Null(viewModel.EmailError);
            Assert.Null(viewModel.PasswordError);
            Assert.Null(viewModel.ConfirmPasswordError);
        }

        [Fact]
        public async Task SignUpAsync_WhenMultipleFieldsInvalid_ShouldSetAllErrorMessages()
        {
            // Arrange
            viewModel.FirstName = "Test123"; // Invalid: contains numbers
            viewModel.LastName = ""; // Invalid: empty
            viewModel.Nickname = "tester";
            viewModel.Email = "invalidemail"; // Invalid: format
            viewModel.Password = "weak"; // Invalid: doesn't meet requirements
            viewModel.ConfirmPassword = "different"; // Invalid: doesn't match

            // Act
            await viewModel.SignUpAsync();

            // Assert
            Assert.NotNull(viewModel.FirstNameError);
            Assert.NotNull(viewModel.LastNameError);
            Assert.NotNull(viewModel.EmailError);
            Assert.NotNull(viewModel.PasswordError);
            Assert.NotNull(viewModel.ConfirmPasswordError);
            
            mockAccountService.Verify(s => s.CreateAccountAsync(It.IsAny<NewAccountDTO>()), Times.Never);
        }

        [Fact]
        public void ValidationErrors_WhenFieldsCorrected_ShouldClearAutomatically()
        {
            // Arrange - Set invalid values
            viewModel.FirstName = "Test123";
            Assert.NotNull(viewModel.FirstNameError);

            // Act - Correct the value
            viewModel.FirstName = "Test";

            // Assert
            Assert.Null(viewModel.FirstNameError);
        }
    }
}
