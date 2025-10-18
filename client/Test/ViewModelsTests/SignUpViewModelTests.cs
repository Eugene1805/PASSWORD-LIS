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
        private readonly Mock<IAccountManagerService> _mockAccountService;
        private readonly Mock<IWindowService> _mockWindowService;
        private readonly SignUpViewModel _viewModel;

        public SignUpViewModelTests()
        {
            _mockAccountService = new Mock<IAccountManagerService>();
            _mockWindowService = new Mock<IWindowService>();
            _viewModel = new SignUpViewModel(_mockAccountService.Object, _mockWindowService.Object);
        }

        [Fact]
        public async Task SignUpCommand_WhenInputIsValidAndServiceSucceeds_ShouldNavigateToVerifyCode()
        {
            // Arrange
            _viewModel.FirstName = "Test";
            _viewModel.LastName = "User";
            _viewModel.Nickname = "tester";
            _viewModel.Email = "test@example.com";
            _viewModel.Password = "ValidPass1!";
            _viewModel.ConfirmPassword = "ValidPass1!";

            _mockAccountService.Setup(s => s.CreateAccountAsync(It.IsAny<NewAccountDTO>()))
                               .Returns(Task.CompletedTask);

            // Act
            await Task.Delay(10); // Small delay to ensure async context
            _viewModel.SignUpCommand.Execute(null);

            // Assert
            _mockAccountService.Verify(s => s.CreateAccountAsync(It.IsAny<NewAccountDTO>()), Times.Once);
            _mockWindowService.Verify(w => w.ShowVerifyCodeWindow(_viewModel.Email, It.IsAny<VerificationReason>()), Times.Once);
            _mockWindowService.Verify(w => w.CloseWindow(_viewModel), Times.Once);
        }

        [Fact]
        public async Task SignUpCommand_WhenServiceThrowsFaultException_ShouldShowUserExistsPopUp()
        {
            // Arrange
            _viewModel.FirstName = "Test";
            _viewModel.LastName = "User";
            _viewModel.Nickname = "tester";
            _viewModel.Email = "test@example.com";
            _viewModel.Password = "ValidPass1!";
            _viewModel.ConfirmPassword = "ValidPass1!";

            _mockAccountService.Setup(s => s.CreateAccountAsync(It.IsAny<NewAccountDTO>()))
                               .ThrowsAsync(new FaultException<ServiceErrorDetailDTO>(new ServiceErrorDetailDTO()));

            // Act
            await Task.Delay(10); // Small delay to ensure async context
            _viewModel.SignUpCommand.Execute(null);

            // Assert
            _mockWindowService.Verify(w => w.ShowPopUp(It.IsAny<string>(), PASSWORD_LIS_Client.Properties.Langs.Lang.userAlreadyExistText, It.IsAny<PopUpIcon>()), Times.Once);
            _mockWindowService.Verify(w => w.ShowVerifyCodeWindow(It.IsAny<string>(), It.IsAny<VerificationReason>()), Times.Never);
        }

        [Fact]
        public void CanExecuteSignUp_WhenFieldsAreEmpty_ShouldBeFalse()
        {
            // Arrange
            _viewModel.Email = ""; // Campo vacío

            // Act
            var canExecute = _viewModel.SignUpCommand.CanExecute(null);

            // Assert
            Assert.False(canExecute);
        }

        [Fact]
        public void NavigateToLoginCommand_ShouldShowLoginAndCloseCurrentWindow()
        {
            // Arrange & Act
            _viewModel.NavigateToLoginCommand.Execute(null);

            // Assert
            _mockWindowService.Verify(w => w.ShowLoginWindow(), Times.Once);
            _mockWindowService.Verify(w => w.CloseWindow(_viewModel), Times.Once);
        }
    }
}
