using Moq;
using PASSWORD_LIS_Client.LoginManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.ViewModels;
using PASSWORD_LIS_Client.Views;
using System.Threading.Tasks;
using Xunit;

namespace Test.ViewModelsTests
{
    public class LoginViewModelTests
    {
        private readonly Mock<ILoginManagerService> mockLoginService;
        private readonly Mock<IWindowService> mockWindowService;
        private readonly LoginViewModel viewModel;

        public LoginViewModelTests()
        {
            mockLoginService = new Mock<ILoginManagerService>();
            mockWindowService = new Mock<IWindowService>();
            viewModel = new LoginViewModel(mockLoginService.Object, mockWindowService.Object);
        }

        [Fact]
        public async Task LoginCommand_WhenCredentialsAreValidAndVerified_ShouldLoginAndShowMainWindow()
        {
            viewModel.Email = "test@example.com";
            viewModel.Password = "ValidPass123";
            var userDto = new UserDTO
            {
                UserAccountId = 1,
                Nickname = "TestUser",
                Email = "test@example.com"
            };

            mockLoginService.Setup(s => s.LoginAsync(viewModel.Email, viewModel.Password))
                .ReturnsAsync(userDto);

            mockLoginService.Setup(s => s.IsAccountVerifiedAsync(viewModel.Email))
                .ReturnsAsync(true);

            await Task.Run(() => viewModel.LoginCommand.Execute(null));

            mockLoginService.Verify(s => s.LoginAsync(viewModel.Email, viewModel.Password), Times.Once);
            mockLoginService.Verify(s => s.IsAccountVerifiedAsync(viewModel.Email), Times.Once);

            mockWindowService.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.successfulLoginText,
                It.IsAny<string>(),
                PopUpIcon.Success), Times.Once);

            mockWindowService.Verify(w => w.ShowMainWindow(), Times.Once);
            mockWindowService.Verify(w => w.CloseWindow(viewModel), Times.Once);
        }

        [Fact]
        public async Task LoginCommand_WhenCredentialsValidButNotVerified_ShouldNavigateToVerification()
        {
            // Arrange
            viewModel.Email = "unverified@example.com";
            viewModel.Password = "ValidPass123";
            var userDto = new UserDTO
            {
                UserAccountId = 2,
                Email = "unverified@example.com"
            };

            mockLoginService.Setup(s => s.LoginAsync(viewModel.Email, viewModel.Password))
                .ReturnsAsync(userDto);

            mockLoginService.Setup(s => s.IsAccountVerifiedAsync(viewModel.Email))
                .ReturnsAsync(false);

            await Task.Run(() => viewModel.LoginCommand.Execute(null));

            mockLoginService.Verify(s => s.SendVerificationCodeAsync(userDto.Email), Times.Once);

            mockWindowService.Verify(w => w.ShowVerifyCodeWindow(
                userDto.Email,
                VerificationReason.AccountActivation), Times.Once);

            mockWindowService.Verify(w => w.CloseWindow(viewModel), Times.Once);
            mockWindowService.Verify(w => w.CloseMainWindow(), Times.Once);
        }

        [Fact]
        public async Task LoginCommand_WhenCredentialsInvalid_ShouldShowWarningPopUp()
        {
            viewModel.Email = "wrong@example.com";
            viewModel.Password = "WrongPass";

            var invalidUser = new UserDTO { UserAccountId = 0 };

            mockLoginService.Setup(s => s.LoginAsync(viewModel.Email, viewModel.Password))
                .ReturnsAsync(invalidUser);

            await Task.Run(() => viewModel.LoginCommand.Execute(null));

            mockWindowService.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.warningTitleText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.wrongCredentialsText,
                PopUpIcon.Warning), Times.Once);

            mockWindowService.Verify(w => w.ShowMainWindow(), Times.Never);
        }

        [Fact]
        public void PlayAsGuestCommand_ShouldLoginAsGuestAndNavigate()
        {
            viewModel.PlayAsGuestCommand.Execute(null);

            mockWindowService.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.successfulLoginText,
                It.IsAny<string>(), 
                PopUpIcon.Success), Times.Once);

            mockWindowService.Verify(w => w.ShowMainWindow(), Times.Once);
            mockWindowService.Verify(w => w.CloseWindow(viewModel), Times.Once);

        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void Email_WhenEmpty_ShouldSetErrorAndDisableLogin(string emptyEmail)
        {
            viewModel.Email = emptyEmail;
            viewModel.Password = "ValidPass";

            var canLogin = viewModel.LoginCommand.CanExecute(null);

            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.emptyEmailText, viewModel.EmailError);
            
            Assert.True(canLogin); 
        }

        [Fact]
        public void Email_WhenTooLong_ShouldSetError()
        {
            viewModel.Email = new string('a', 101); 

            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.emailTooLongText, viewModel.EmailError);
        }

        [Fact]
        public void Email_WhenValid_ShouldClearError()
        {
            viewModel.Email = "valid@test.com";

            Assert.Null(viewModel.EmailError);
        }

        [Fact]
        public void Password_WhenEmpty_ShouldSetError()
        {
            viewModel.Password = "";

            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.emptyPasswordText, viewModel.PasswordError);
        }

        [Fact]
        public void Password_WhenTooLong_ShouldSetError()
        {
            viewModel.Password = "1234567890123456"; 

            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.passwordTooLongText, viewModel.PasswordError);
        }

        [Fact]
        public async Task LoginAsync_WhenFieldsInvalid_ShouldNotCallService()
        {
            viewModel.Email = ""; 
            viewModel.Password = ""; 

            await Task.Run(() => viewModel.LoginCommand.Execute(null));

            mockLoginService.Verify(s => s.LoginAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void IsLoggingIn_ShouldPreventReentrancy()
        {
            viewModel.IsLoggingIn = true;

            var canExecute = viewModel.LoginCommand.CanExecute(null);

            Assert.False(canExecute);
        }
    }
}