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

            mockAccountService.Setup(s => s.CreateAccountAsync(It.IsAny<NewAccountDTO>()))
                               .Returns(Task.CompletedTask);

            // Act
            await Task.Delay(10); // Small delay to ensure async context
            viewModel.SignUpCommand.Execute(null);

            // Assert
            mockAccountService.Verify(s => s.CreateAccountAsync(It.IsAny<NewAccountDTO>()), Times.Once);
            mockWindowService.Verify(w => w.ShowVerifyCodeWindow(viewModel.Email, It.IsAny<VerificationReason>()), Times.Once);
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

            mockAccountService.Setup(s => s.CreateAccountAsync(It.IsAny<NewAccountDTO>()))
                               .ThrowsAsync(new FaultException<ServiceErrorDetailDTO>(new ServiceErrorDetailDTO()));

            // Act
            await Task.Delay(10); // Small delay to ensure async context
            viewModel.SignUpCommand.Execute(null);

            // Assert
            mockWindowService.Verify(w => w.ShowPopUp(It.IsAny<string>(), PASSWORD_LIS_Client.Properties.Langs.Lang.userAlreadyExistText, It.IsAny<PopUpIcon>()), Times.Once);
            mockWindowService.Verify(w => w.ShowVerifyCodeWindow(It.IsAny<string>(), It.IsAny<VerificationReason>()), Times.Never);
        }

        [Fact]
        public void CanExecuteSignUp_WhenFieldsAreEmpty_ShouldBeFalse()
        {
            // Arrange
            viewModel.Email = ""; // Campo vacío

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
    }
}
