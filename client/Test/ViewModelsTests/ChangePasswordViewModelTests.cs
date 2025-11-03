using Moq;
using PASSWORD_LIS_Client.PasswordResetManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.ViewModels;
using PASSWORD_LIS_Client.Views;
using System;
using System.ServiceModel;
using Xunit;

namespace Test.ViewModelsTests
{
    public class ChangePasswordViewModelTests
    {
        [Fact]
        public void CanChangePassword_WhenEmptyOrBusy_ShouldBeFalse()
        {
            var mockWindow = new Mock<IWindowService>();
            var mockClient = new Mock<IPasswordResetManagerService>();
            var vm = new ChangePasswordViewModel("e@e.com", "code", mockWindow.Object, mockClient.Object);
            vm.NewPassword = string.Empty;
            vm.ConfirmPassword = string.Empty;
            Assert.False(vm.ChangePasswordCommand.CanExecute(null));
            vm.NewPassword = "x";
            vm.ConfirmPassword = "y";
            vm.IsBusy = true;
            Assert.False(vm.ChangePasswordCommand.CanExecute(null));
        }

        [Fact]
        public void ChangePassword_WhenPasswordsDontMatch_ShouldShowWarning()
        {
            var mockWindow = new Mock<IWindowService>();
            var mockClient = new Mock<IPasswordResetManagerService>();
            var vm = new ChangePasswordViewModel("e@e.com", "code", mockWindow.Object, mockClient.Object)
            {
                NewPassword = "abc",
                ConfirmPassword = "xyz"
            };

            vm.ChangePasswordCommand.Execute(null);

            mockWindow.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.warningTitleText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.matchingPasswordErrorText,
                It.IsAny<PopUpIcon>()), Times.Once);
            mockClient.Verify(c => c.ResetPasswordAsync(It.IsAny<PasswordResetDTO>()), Times.Never);
        }

        [Fact]
        public void ChangePassword_WhenServiceSuccess_ShouldShowSuccessAndNavigateLoginAndClose()
        {
            var mockWindow = new Mock<IWindowService>();
            var mockClient = new Mock<IPasswordResetManagerService>();
            mockClient.Setup(c => c.ResetPasswordAsync(It.IsAny<PasswordResetDTO>())).ReturnsAsync(true);
            var vm = new ChangePasswordViewModel("e@e.com", "code", mockWindow.Object, mockClient.Object)
            {
                NewPassword = "Valid1!",
                ConfirmPassword = "Valid1!"
            };

            vm.ChangePasswordCommand.Execute(null);

            mockWindow.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.succesfulPasswordChangeTitleText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.successfulPasswordChangeText,
                It.IsAny<PopUpIcon>()), Times.Once);
            mockWindow.Verify(w => w.ShowLoginWindow(), Times.Once);
            mockWindow.Verify(w => w.CloseWindow(vm), Times.Once);
        }

        [Fact]
        public void ChangePassword_WhenServiceFails_ShouldShowWarning()
        {
            var mockWindow = new Mock<IWindowService>();
            var mockClient = new Mock<IPasswordResetManagerService>();
            mockClient.Setup(c => c.ResetPasswordAsync(It.IsAny<PasswordResetDTO>())).ReturnsAsync(false);
            var vm = new ChangePasswordViewModel("e@e.com", "code", mockWindow.Object, mockClient.Object)
            {
                NewPassword = "Valid1!",
                ConfirmPassword = "Valid1!"
            };

            vm.ChangePasswordCommand.Execute(null);

            mockWindow.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.unexpectedErrorText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.passwordChangeFailedText,
                It.IsAny<PopUpIcon>()), Times.Once);
        }

        [Fact]
        public void ChangePassword_WhenTimeout_ShouldShowTimeoutWarning()
        {
            var mockWindow = new Mock<IWindowService>();
            var mockClient = new Mock<IPasswordResetManagerService>();
            mockClient
                .Setup(c => c.ResetPasswordAsync(It.IsAny<PasswordResetDTO>()))
                .ThrowsAsync(new TimeoutException());
            var vm = new ChangePasswordViewModel("e@e.com", "code", mockWindow.Object, mockClient.Object)
            {
                NewPassword = "Valid1!",
                ConfirmPassword = "Valid1!"
            };

            vm.ChangePasswordCommand.Execute(null);

            mockWindow.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.timeLimitTitleText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.serverTimeoutText,
                It.IsAny<PopUpIcon>()), Times.Once);
        }

        [Fact]
        public void ChangePassword_WhenEndpointNotFound_ShouldShowConnectionWarning()
        {
            var mockWindow = new Mock<IWindowService>();
            var mockClient = new Mock<IPasswordResetManagerService>();
            mockClient
                .Setup(c => c.ResetPasswordAsync(It.IsAny<PasswordResetDTO>()))
                .ThrowsAsync(new EndpointNotFoundException());
            var vm = new ChangePasswordViewModel("e@e.com", "code", mockWindow.Object, mockClient.Object)
            {
                NewPassword = "Valid1!",
                ConfirmPassword = "Valid1!"
            };

            vm.ChangePasswordCommand.Execute(null);

            mockWindow.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.connectionErrorTitleText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.serverConnectionInternetErrorText,
                It.IsAny<PopUpIcon>()), Times.Once);
        }
    }
}
