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

            // Mock IsNicknameInUseAsync to return false (nickname is available)
            mockAccountService.Setup(s => s.IsNicknameInUseAsync(It.IsAny<string>()))
                               .ReturnsAsync(false);

            mockAccountService.Setup(s => s.CreateAccountAsync(It.IsAny<NewAccountDTO>()))
                               .Returns(Task.CompletedTask);

            // Act
            await viewModel.SignUpAsync();

            // Assert
            mockAccountService.Verify(s => s.IsNicknameInUseAsync(It.IsAny<string>()), Times.Once);
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

            // Mock IsNicknameInUseAsync to return false (nickname is available)
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
