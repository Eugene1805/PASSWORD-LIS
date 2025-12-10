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
            Assert.False(vm.VerifyCodeCommand.CanExecute(null));
            vm.EnteredCode = "123456";
            vm.IsBusy = true;
            Assert.False(vm.VerifyCodeCommand.CanExecute(null));
        }

        [Fact]
        public void Verify_WhenEmpty_ShouldNotExecute()
        {
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
            vm.VerifyCodeCommand.Execute(null);
            mockNewAcc.Verify(s => s.VerifyEmailAsync(It.IsAny<EmailVerificationDTO>()), Times.Never);
        }

        [Fact]
        public void Verify_AccountActivation_Success_ShouldShowSuccessAndNavigateLogin()
        {
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
                EnteredCode = "999999"
            };
            vm.VerifyCodeCommand.Execute(null);
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
                EnteredCode = "999999"
            };
            vm.VerifyCodeCommand.Execute(null);
            mockWin.Verify(w => w.ShowChangePasswordWindow("user@ex.com", "999999"), Times.Once);
            mockWin.Verify(w => w.CloseWindow(vm), Times.Once);
        }

        [Fact]
        public void Verify_WhenInvalid_ShouldSetErrorProperty()
        {
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
                EnteredCode = "000000"
            };
            vm.VerifyCodeCommand.Execute(null);
            Assert.NotNull(vm.EnteredCodeError);
            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.codeIncorrectOrExpiredText, vm.EnteredCodeError);
        }

        [Fact]
        public void Resend_WhenAccountActivation_Success_ShouldShowInfoPopup()
        {
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
            vm.ResendCodeCommand.Execute(null);
            mockWin.Verify(w => w.ShowPopUp(
            PASSWORD_LIS_Client.Properties.Langs.Lang.codeSentTitleText,
            PASSWORD_LIS_Client.Properties.Langs.Lang.newCodeSentText,
            It.IsAny<PopUpIcon>()), Times.Once);
        }

        [Fact]
        public void Resend_WhenPasswordReset_Failure_ShouldWarn()
        {
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
            vm.ResendCodeCommand.Execute(null);
            mockWin.Verify(w => w.ShowPopUp(
            PASSWORD_LIS_Client.Properties.Langs.Lang.timeLimitTitleText,
            PASSWORD_LIS_Client.Properties.Langs.Lang.waitAMinuteForCodeText,
            It.IsAny<PopUpIcon>()), Times.Once);
        }

        [Fact]
        public void Verify_WhenTimeout_ShouldShowTimeoutWarning()
        {
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
                EnteredCode = "111111"
            };
            vm.VerifyCodeCommand.Execute(null);
            mockWin.Verify(w => w.ShowPopUp(
            PASSWORD_LIS_Client.Properties.Langs.Lang.timeLimitTitleText,
            PASSWORD_LIS_Client.Properties.Langs.Lang.serverTimeoutText,
            It.IsAny<PopUpIcon>()), Times.Once);
        }

        [Fact]
        public void EnteredCode_WhenEmpty_ShouldSetError()
        {
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
            Assert.NotNull(vm.EnteredCodeError);
            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.requiredFieldsText, vm.EnteredCodeError);
        }

        [Fact]
        public void EnteredCode_WhenLessThanSixDigits_ShouldSetError()
        {
            var mockWin = new Mock<IWindowService>();
            var mockNewAcc = new Mock<IVerificationCodeManagerService>();
            var mockReset = new Mock<IPasswordResetManagerService>();
            var vm = new VerifyCodeViewModel(
            "user@ex.com",
            PASSWORD_LIS_Client.Views.VerificationReason.AccountActivation,
            mockWin.Object,
            mockNewAcc.Object,
            mockReset.Object);
            vm.EnteredCode = "12345";
            Assert.NotNull(vm.EnteredCodeError);
            Assert.Equal(PASSWORD_LIS_Client.Properties.Langs.Lang.codeIncorrectOrExpiredText, vm.EnteredCodeError);
        }

        [Fact]
        public void EnteredCode_WhenValid_ShouldClearError()
        {
            var mockWin = new Mock<IWindowService>();
            var mockNewAcc = new Mock<IVerificationCodeManagerService>();
            var mockReset = new Mock<IPasswordResetManagerService>();
            var vm = new VerifyCodeViewModel(
            "user@ex.com",
            PASSWORD_LIS_Client.Views.VerificationReason.AccountActivation,
            mockWin.Object,
            mockNewAcc.Object,
            mockReset.Object);
            vm.EnteredCode = "123456";
            Assert.Null(vm.EnteredCodeError);
        }

        [Fact]
        public void CanVerify_WhenCodeInvalid_ShouldBeFalse()
        {
            var mockWin = new Mock<IWindowService>();
            var mockNewAcc = new Mock<IVerificationCodeManagerService>();
            var mockReset = new Mock<IPasswordResetManagerService>();
            var vm = new VerifyCodeViewModel(
            "user@ex.com",
            PASSWORD_LIS_Client.Views.VerificationReason.AccountActivation,
            mockWin.Object,
            mockNewAcc.Object,
            mockReset.Object);
            vm.EnteredCode = "12345";
            Assert.False(vm.VerifyCodeCommand.CanExecute(null));
        }

        [Fact]
        public void CanVerify_WhenCodeValid_ShouldBeTrue()
        {
            var mockWin = new Mock<IWindowService>();
            var mockNewAcc = new Mock<IVerificationCodeManagerService>();
            var mockReset = new Mock<IPasswordResetManagerService>();
            var vm = new VerifyCodeViewModel(
            "user@ex.com",
            PASSWORD_LIS_Client.Views.VerificationReason.AccountActivation,
            mockWin.Object,
            mockNewAcc.Object,
            mockReset.Object);
            vm.EnteredCode = "123456";
            Assert.True(vm.VerifyCodeCommand.CanExecute(null));
        }
    }
}
