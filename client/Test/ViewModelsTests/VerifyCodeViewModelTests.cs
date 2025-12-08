using Moq;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.ViewModels;
using PASSWORD_LIS_Client.VerificationCodeManagerServiceReference;
using PASSWORD_LIS_Client.Views;
using System;
using Xunit;

namespace Test.ViewModelsTests
{
    public class VerifyCodeViewModelTests
    {
        [Fact]
        public void CanVerify_BlocksWhenBusyOrEmpty()
        {
            // Arrange
            var mockWin = new Mock<IWindowService>();
            var mockNewAcc = new Mock<IVerificationCodeManagerService>();
            var mockReset = new Mock<IPasswordResetManagerService>();
            var vm = new VerifyCodeViewModel(
            "user@ex.com",
            PASSWORD_LIS_Client.Views.VerificationReason.AccountActivation,
            mockWin.Object,
            mockNewAcc.Object,
            mockReset.Object);
            vm.EnteredCode = string.Empty;
            // Act
            // Assert
            Assert.False(vm.VerifyCodeCommand.CanExecute(null));
            vm.EnteredCode = "1234";
            vm.IsBusy = true;
            Assert.False(vm.VerifyCodeCommand.CanExecute(null));
        }

        [Fact]
        public void Verify_WhenEmpty_ShouldShowRequiredPopUp()
        {
            // Arrange
            var mockWin = new Mock<IWindowService>();
            var mockNewAcc = new Mock<IVerificationCodeManagerService>();
            var mockReset = new Mock<IPasswordResetManagerService>();
            var vm = new VerifyCodeViewModel(
            "user@ex.com",
            PASSWORD_LIS_Client.Views.VerificationReason.AccountActivation,
            mockWin.Object,
            mockNewAcc.Object,
            mockReset.Object);
            vm.EnteredCode = string.Empty;
            // Act
            vm.VerifyCodeCommand.Execute(null);
            // Assert
            mockWin.Verify(w => w.ShowPopUp(
            PASSWORD_LIS_Client.Properties.Langs.Lang.codeWordText,
            PASSWORD_LIS_Client.Properties.Langs.Lang.requiredFieldsText,
            It.IsAny<PopUpIcon>()), Times.Once);
        }

        [Fact]
        public void Verify_AccountActivation_Success_ShouldShowSuccessAndNavigateLogin()
        {
            // Arrange
            var mockWin = new Mock<IWindowService>();
            var mockNewAcc = new Mock<IVerificationCodeManagerService>();
            var mockReset = new Mock<IPasswordResetManagerService>();
            mockNewAcc
            .Setup(s => s.VerifyEmailAsync(It.IsAny<EmailVerificationDTO>()))
            .ReturnsAsync(true);
            var vm = new VerifyCodeViewModel(
            "user@ex.com",
            PASSWORD_LIS_Client.Views.VerificationReason.AccountActivation,
            mockWin.Object,
            mockNewAcc.Object,
            mockReset.Object)
            {
                EnteredCode = "9999"
            };

            // Act
            vm.VerifyCodeCommand.Execute(null);

            // Assert
            mockWin.Verify(w => w.ShowPopUp(
            PASSWORD_LIS_Client.Properties.Langs.Lang.profileUpdatedTitleText,
            PASSWORD_LIS_Client.Properties.Langs.Lang.successfulSignUpText,
            It.IsAny<PopUpIcon>()), Times.Once);
            mockWin.Verify(w => w.ShowLoginWindow(), Times.Once);
            mockWin.Verify(w => w.CloseWindow(vm), Times.Once);
        }

        [Fact]
        public void Verify_PasswordReset_Success_ShouldOpenChangePassword()
        {
            // Arrange
            var mockWin = new Mock<IWindowService>();
            var mockNewAcc = new Mock<IVerificationCodeManagerService>();
            var mockReset = new Mock<IPasswordResetManagerService>();
            mockReset
            .Setup(s => s.ValidatePasswordResetCodeAsync(
                It.IsAny<PASSWORD_LIS_Client.PasswordResetManagerServiceReference.EmailVerificationDTO>()))
            .ReturnsAsync(true);
            var vm = new VerifyCodeViewModel(
            "user@ex.com",
            PASSWORD_LIS_Client.Views.VerificationReason.PasswordReset,
            mockWin.Object,
            mockNewAcc.Object,
            mockReset.Object)
            {
                EnteredCode = "9999"
            };

            // Act
            vm.VerifyCodeCommand.Execute(null);

            // Assert
            mockWin.Verify(w => w.ShowChangePasswordWindow("user@ex.com", "9999"), Times.Once);
            mockWin.Verify(w => w.CloseWindow(vm), Times.Once);
        }

        [Fact]
        public void Verify_WhenInvalid_ShouldShowErrorPopUp()
        {
            // Arrange
            var mockWin = new Mock<IWindowService>();
            var mockNewAcc = new Mock<IVerificationCodeManagerService>();
            var mockReset = new Mock<IPasswordResetManagerService>();
            mockNewAcc
            .Setup(s => s.VerifyEmailAsync(It.IsAny<EmailVerificationDTO>()))
            .ReturnsAsync(false);
            var vm = new VerifyCodeViewModel(
            "user@ex.com",
            PASSWORD_LIS_Client.Views.VerificationReason.AccountActivation,
            mockWin.Object,
            mockNewAcc.Object,
            mockReset.Object)
            {
                EnteredCode = "0000"
            };

            // Act
            vm.VerifyCodeCommand.Execute(null);

            // Assert
            mockWin.Verify(w => w.ShowPopUp(
            PASSWORD_LIS_Client.Properties.Langs.Lang.verificationFailedTitleText,
            PASSWORD_LIS_Client.Properties.Langs.Lang.codeIncorrectOrExpiredText,
            It.IsAny<PopUpIcon>()), Times.Once);
        }

        [Fact]
        public void Resend_WhenAccountActivation_Success_ShouldShowInfoPopup()
        {
            // Arrange
            var mockWin = new Mock<IWindowService>();
            var mockNewAcc = new Mock<IVerificationCodeManagerService>();
            var mockReset = new Mock<IPasswordResetManagerService>();
            mockNewAcc.Setup(s => s.ResendVerificationCodeAsync("user@ex.com")).ReturnsAsync(true);
            var vm = new VerifyCodeViewModel(
            "user@ex.com",
            PASSWORD_LIS_Client.Views.VerificationReason.AccountActivation,
            mockWin.Object,
            mockNewAcc.Object,
            mockReset.Object);

            // Act
            vm.ResendCodeCommand.Execute(null);

            // Assert
            mockWin.Verify(w => w.ShowPopUp(
            PASSWORD_LIS_Client.Properties.Langs.Lang.codeSentTitleText,
            PASSWORD_LIS_Client.Properties.Langs.Lang.newCodeSentText,
            It.IsAny<PopUpIcon>()), Times.Once);
        }

        [Fact]
        public void Resend_WhenPasswordReset_Failure_ShouldWarn()
        {
            // Arrange
            var mockWin = new Mock<IWindowService>();
            var mockNewAcc = new Mock<IVerificationCodeManagerService>();
            var mockReset = new Mock<IPasswordResetManagerService>();
            mockReset
            .Setup(s => s.RequestPasswordResetCodeAsync(
                It.IsAny<PASSWORD_LIS_Client.PasswordResetManagerServiceReference.EmailVerificationDTO>()))
            .ReturnsAsync(false);
            var vm = new VerifyCodeViewModel(
            "user@ex.com",
            PASSWORD_LIS_Client.Views.VerificationReason.PasswordReset,
            mockWin.Object,
            mockNewAcc.Object,
            mockReset.Object);

            // Act
            vm.ResendCodeCommand.Execute(null);

            // Assert
            mockWin.Verify(w => w.ShowPopUp(
            PASSWORD_LIS_Client.Properties.Langs.Lang.timeLimitTitleText,
            PASSWORD_LIS_Client.Properties.Langs.Lang.waitAMinuteForCodeText,
            It.IsAny<PopUpIcon>()), Times.Once);
        }

        [Fact]
        public void Verify_WhenTimeout_ShouldShowTimeoutWarning()
        {
            // Arrange
            var mockWin = new Mock<IWindowService>();
            var mockNewAcc = new Mock<IVerificationCodeManagerService>();
            var mockReset = new Mock<IPasswordResetManagerService>();
            mockNewAcc
            .Setup(s => s.VerifyEmailAsync(It.IsAny<EmailVerificationDTO>()))
            .ThrowsAsync(new TimeoutException());
            var vm = new VerifyCodeViewModel(
            "user@ex.com",
            PASSWORD_LIS_Client.Views.VerificationReason.AccountActivation,
            mockWin.Object,
            mockNewAcc.Object,
            mockReset.Object)
            {
                EnteredCode = "1111"
            };

            // Act
            vm.VerifyCodeCommand.Execute(null);

            // Assert
            mockWin.Verify(w => w.ShowPopUp(
            PASSWORD_LIS_Client.Properties.Langs.Lang.timeLimitTitleText,
            PASSWORD_LIS_Client.Properties.Langs.Lang.serverTimeoutText,
            It.IsAny<PopUpIcon>()), Times.Once);
        }
    }
}
