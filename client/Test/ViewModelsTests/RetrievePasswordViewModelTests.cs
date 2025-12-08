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
    public class RetrievePasswordViewModelTests
    {
        [Fact]
        public void CanSendCode_WhenEmptyOrBusy_ShouldBeFalse()
        {
            // Arrange
            var mockClient = new Mock<IPasswordResetManagerService>();
            var mockWindow = new Mock<IWindowService>();
            var vm = new RetrievePasswordViewModel(mockClient.Object, mockWindow.Object);
            vm.Email = string.Empty;
            // Act
            // Assert
            Assert.False(vm.SendCodeCommand.CanExecute(null));
            vm.Email = "test@example.com";
            vm.IsBusy = true;
            Assert.False(vm.SendCodeCommand.CanExecute(null));
        }

        [Fact]
        public void SendCode_WhenInvalidEmail_ShouldShowPopUpAndNotCallService()
        {
            // Arrange
            var mockClient = new Mock<IPasswordResetManagerService>();
            var mockWindow = new Mock<IWindowService>();
            var vm = new RetrievePasswordViewModel(mockClient.Object, mockWindow.Object);
            vm.Email = "invalid";

            // Act
            vm.SendCodeCommand.Execute(null);

            // Assert
            mockWindow.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.warningTitleText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.invalidEmailErrorText,
                It.IsAny<PopUpIcon>()), Times.Once);
            mockClient.Verify(c => c.RequestPasswordResetCodeAsync(It.IsAny<EmailVerificationDTO>()), Times.Never);
        }

        [Fact]
        public void SendCode_WhenServiceSuccess_ShouldOpenVerifyAndClose()
        {
            // Arrange
            var mockClient = new Mock<IPasswordResetManagerService>();
            var mockWindow = new Mock<IWindowService>();
            mockClient
                .Setup(c => c.RequestPasswordResetCodeAsync(It.IsAny<EmailVerificationDTO>()))
                .ReturnsAsync(true);
            var vm = new RetrievePasswordViewModel(mockClient.Object, mockWindow.Object)
            {
                Email = "user@example.com"
            };

            // Act
            vm.SendCodeCommand.Execute(null);

            // Assert
            mockWindow.Verify(w => w.ShowVerifyCodeWindow(
                "user@example.com",
                PASSWORD_LIS_Client.Views.VerificationReason.PasswordReset), Times.Once);
            mockWindow.Verify(w => w.CloseWindow(vm), Times.Once);
        }

        [Fact]
        public void SendCode_WhenServiceFails_ShouldShowErrorPopUp()
        {
            // Arrange
            var mockClient = new Mock<IPasswordResetManagerService>();
            var mockWindow = new Mock<IWindowService>();
            mockClient
                .Setup(c => c.RequestPasswordResetCodeAsync(It.IsAny<EmailVerificationDTO>()))
                .ReturnsAsync(false);
            var vm = new RetrievePasswordViewModel(mockClient.Object, mockWindow.Object)
            {
                Email = "user@example.com"
            };

            // Act
            vm.SendCodeCommand.Execute(null);

            // Assert
            mockWindow.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.sendFailedTitleText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.codeSendFailedText,
                It.IsAny<PopUpIcon>()), Times.Once);
        }

        [Fact]
        public void TryRequestResetCode_WhenTimeout_ShouldShowTimeoutWarningAndReturnFalse()
        {
            // Arrange
            var mockClient = new Mock<IPasswordResetManagerService>();
            var mockWindow = new Mock<IWindowService>();
            mockClient
                .Setup(c => c.RequestPasswordResetCodeAsync(It.IsAny<EmailVerificationDTO>()))
                .ThrowsAsync(new TimeoutException());
            var vm = new RetrievePasswordViewModel(mockClient.Object, mockWindow.Object)
            {
                Email = "user@example.com"
            };

            // Act
            vm.SendCodeCommand.Execute(null);

            // Assert
            mockWindow.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.timeLimitTitleText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.serverTimeoutText,
                It.IsAny<PopUpIcon>()), Times.Once);
        }

        [Fact]
        public void TryRequestResetCode_WhenEndpointNotFound_ShouldShowConnectionWarning()
        {
            // Arrange
            var mockClient = new Mock<IPasswordResetManagerService>();
            var mockWindow = new Mock<IWindowService>();
            mockClient
                .Setup(c => c.RequestPasswordResetCodeAsync(It.IsAny<EmailVerificationDTO>()))
                .ThrowsAsync(new EndpointNotFoundException());
            var vm = new RetrievePasswordViewModel(mockClient.Object, mockWindow.Object)
            {
                Email = "user@example.com"
            };

            // Act
            vm.SendCodeCommand.Execute(null);

            // Assert
            mockWindow.Verify(w => w.ShowPopUp(
                PASSWORD_LIS_Client.Properties.Langs.Lang.connectionErrorTitleText,
                PASSWORD_LIS_Client.Properties.Langs.Lang.serverConnectionInternetErrorText,
                It.IsAny<PopUpIcon>()), Times.Once);
        }
    }
}
