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

            await viewModel.SignUpAsync();

            mockAccountService.Verify(s => s.IsNicknameInUseAsync(It.IsAny<string>()), Times.Once);
            mockAccountService.Verify(s => s.CreateAccountAsync(It.IsAny<NewAccountDTO>()), Times.Once);
            mockWindowService.Verify(w => w.ShowVerifyCodeWindow(viewModel.Email, It.IsAny<VerificationReason>()), 
                Times.Once);
            mockWindowService.Verify(w => w.CloseWindow(viewModel), Times.Once);
        }

        [Fact]
        public async Task SignUpCommand_WhenServiceThrowsFaultException_ShouldShowUserExistsPopUp()
        {
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

            await viewModel.SignUpAsync();

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
            viewModel.Email = "";

            var canExecute = viewModel.SignUpCommand.CanExecute(null);

            Assert.False(canExecute);
        }

        [Fact]
        public void NavigateToLoginCommand_ShouldShowLoginAndCloseCurrentWindow()
        {
            viewModel.NavigateToLoginCommand.Execute(null);

            mockWindowService.Verify(w => w.ShowLoginWindow(), Times.Once);
            mockWindowService.Verify(w => w.CloseWindow(viewModel), Times.Once);
        }

        [Theory]
        [InlineData("", "emptyFirstNameText")]
        [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "firstNameTooLongText")]
        [InlineData("Test123", "nameInvalidCharsText")]
        public void FirstName_WhenInvalid_ShouldSetErrorMessage(string value, string errorKey)
        {
            viewModel.FirstName = value;

            var expectedError = typeof(PASSWORD_LIS_Client.Properties.Langs.Lang)
                .GetProperty(errorKey)
                .GetValue(null) as string;
            Assert.Equal(expectedError, viewModel.FirstNameError);
        }

        [Fact]
        public void FirstName_WhenValid_ShouldClearErrorMessage()
        {
            viewModel.FirstName = "ValidName";

            Assert.Null(viewModel.FirstNameError);
        }

        [Theory]
        [InlineData("", "emptyLastNameText")]
        [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "lastNameTooLongText")]
        [InlineData("Test123", "lastNameInvalidCharsText")]
        public void LastName_WhenInvalid_ShouldSetErrorMessage(string value, string errorKey)
        {
            viewModel.LastName = value;

            var expectedError = typeof(PASSWORD_LIS_Client.Properties.Langs.Lang)
                .GetProperty(errorKey)
                .GetValue(null) as string;
            Assert.Equal(expectedError, viewModel.LastNameError);
        }

        [Fact]
        public void LastName_WhenValid_ShouldClearErrorMessage()
        {
            viewModel.LastName = "ValidLastName";

            Assert.Null(viewModel.LastNameError);
        }

        [Theory]
        [InlineData("", "emptyNicknameText")]
        [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "nicknameTooLongText")]
        public void Nickname_WhenInvalid_ShouldSetErrorMessage(string value, string errorKey)
        {
            viewModel.Nickname = value;

            var expectedError = typeof(PASSWORD_LIS_Client.Properties.Langs.Lang)
                .GetProperty(errorKey)
                .GetValue(null) as string;
            Assert.Equal(expectedError, viewModel.NicknameError);
        }

        [Fact]
        public void Nickname_WhenValid_ShouldClearErrorMessage()
        {
            viewModel.Nickname = "ValidNickname";

            Assert.Null(viewModel.NicknameError);
        }

        [Fact]
        public async Task SignUpAsync_WhenNicknameInUse_ShouldSetNicknameError()
        {
            viewModel.FirstName = "Test";
            viewModel.LastName = "User";
            viewModel.Nickname = "existingNickname";
            viewModel.Email = "test@example.com";
            viewModel.Password = "ValidPass1!";
            viewModel.ConfirmPassword = "ValidPass1!";

            mockAccountService.Setup(s => s.IsNicknameInUseAsync(It.IsAny<string>()))
                               .ReturnsAsync(true);

            await viewModel.SignUpAsync();

            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.nicknameInUseText, viewModel.NicknameError);
            mockAccountService.Verify(s => s.CreateAccountAsync(It.IsAny<NewAccountDTO>()), Times.Never);
        }

        [Theory]
        [InlineData("")]
        [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        public void Email_WhenEmptyOrTooLong_ShouldSetErrorMessage(string value)
        {
            viewModel.Email = value;

            Assert.NotNull(viewModel.EmailError);
        }

        [Fact]
        public void Email_WhenInvalidFormat_ShouldSetErrorMessage()
        {
            viewModel.Email = "invalidemail";

            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.invalidEmailFormatText, viewModel.EmailError);
        }

        [Fact]
        public void Email_WhenValid_ShouldClearErrorMessage()
        {
            viewModel.Email = "valid@email.com";

            Assert.Null(viewModel.EmailError);
        }

        [Theory]
        [InlineData("")]
        [InlineData("weak")]
        public void Password_WhenInvalid_ShouldSetErrorMessage(string value)
        {
            viewModel.Password = value;

            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.userPasswordRequirementsText, viewModel.PasswordError);
        }

        [Fact]
        public void Password_WhenValid_ShouldClearErrorMessage()
        {
            viewModel.Password = "ValidPass1!";

            Assert.Null(viewModel.PasswordError);
        }

        [Theory]
        [InlineData("")]
        [InlineData("DifferentPass1!")]
        public void ConfirmPassword_WhenInvalid_ShouldSetErrorMessage(string confirmPassword)
        {
            viewModel.Password = "ValidPass1!";

            viewModel.ConfirmPassword = confirmPassword;

            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.matchingPasswordErrorText, viewModel.ConfirmPasswordError);
        }

        [Fact]
        public void ConfirmPassword_WhenMatchesPassword_ShouldClearErrorMessage()
        {
            viewModel.Password = "ValidPass1!";
            viewModel.ConfirmPassword = "ValidPass1!";

            Assert.Null(viewModel.ConfirmPasswordError);
        }

        [Fact]
        public void Password_WhenChangedAfterConfirmPassword_ShouldRevalidateConfirmPassword()
        {
            viewModel.Password = "ValidPass1!";
            viewModel.ConfirmPassword = "ValidPass1!";
            Assert.Null(viewModel.ConfirmPasswordError);

            viewModel.Password = "NewValidPass1!";

            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.matchingPasswordErrorText, viewModel.ConfirmPasswordError);
        }

        [Fact]
        public void CanExecuteSignUp_WhenAllFieldsValidAndFilled_ShouldReturnTrue()
        {
            viewModel.FirstName = "Test";
            viewModel.LastName = "User";
            viewModel.Nickname = "tester";
            viewModel.Email = "test@example.com";
            viewModel.Password = "ValidPass1!";
            viewModel.ConfirmPassword = "ValidPass1!";

            var canExecute = viewModel.SignUpCommand.CanExecute(null);

            Assert.True(canExecute);
        }

        [Fact]
        public void CanExecuteSignUp_WhenAnyFieldHasError_ShouldReturnFalse()
        {
            viewModel.FirstName = "Test123";
            viewModel.LastName = "User";
            viewModel.Nickname = "tester";
            viewModel.Email = "test@example.com";
            viewModel.Password = "ValidPass1!";
            viewModel.ConfirmPassword = "ValidPass1!";

            var canExecute = viewModel.SignUpCommand.CanExecute(null);

            Assert.False(canExecute);
        }

        [Fact]
        public void CanExecuteSignUp_WhenIsSigningUp_ShouldReturnFalse()
        {
            viewModel.FirstName = "Test";
            viewModel.LastName = "User";
            viewModel.Nickname = "tester";
            viewModel.Email = "test@example.com";
            viewModel.Password = "ValidPass1!";
            viewModel.ConfirmPassword = "ValidPass1!";

            typeof(SignUpViewModel).GetProperty(nameof(viewModel.IsSigningUp))
                .SetValue(viewModel, true);

            var canExecute = viewModel.SignUpCommand.CanExecute(null);

            Assert.False(canExecute);
        }

        [Fact]
        public async Task SignUpAsync_WhenAllFieldsValid_ShouldNotShowAnyPopUp()
        {
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

            await viewModel.SignUpAsync();

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
            viewModel.FirstName = "Test123";
            viewModel.LastName = "";
            viewModel.Nickname = "tester";
            viewModel.Email = "invalidemail";
            viewModel.Password = "weak";
            viewModel.ConfirmPassword = "different";

            await viewModel.SignUpAsync();

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
            viewModel.FirstName = "Test123";
            Assert.NotNull(viewModel.FirstNameError);

            viewModel.FirstName = "Test";

            Assert.Null(viewModel.FirstNameError);
        }
    }
}
