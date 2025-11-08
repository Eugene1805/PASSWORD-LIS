using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.VerificationCodeManagerServiceReference;
using PASSWORD_LIS_Client.Views;
using System;
using System.ServiceModel;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class VerifyCodeViewModel : BaseViewModel
    {
        private readonly VerificationReason reason;
        private readonly IWindowService windowService;
        private readonly IVerificationCodeManagerService newAccountClient;
        private readonly IPasswordResetManagerService resetPasswordClient;

        public string Email { get; }
        private string enteredCode;
        public string EnteredCode
        {
            get => this.enteredCode;
            set { this.enteredCode = value; OnPropertyChanged(); }
        }

        private bool isBusy;
        public bool IsBusy
        {
            get => this.isBusy;
            set { this.isBusy = value; OnPropertyChanged(); }
        }

        public RelayCommand VerifyCodeCommand { get; }
        public RelayCommand ResendCodeCommand { get; }

        public VerifyCodeViewModel(string email, VerificationReason reason, IWindowService windowService,
            IVerificationCodeManagerService verificationCodeManager, IPasswordResetManagerService passwordResetManager)
        {
            this.Email = email;
            this.reason = reason;
            this.windowService = windowService;
            this.newAccountClient = verificationCodeManager;
            this.resetPasswordClient = passwordResetManager;

            this.VerifyCodeCommand = new RelayCommand(async (_) => await VerifyCodeAsync(), (_) => CanVerify());
            this.ResendCodeCommand = new RelayCommand(async (_) => await ResendCodeAsync(), (_) => !this.IsBusy);
        }
        private bool CanVerify()
        {
            return !this.IsBusy && !string.IsNullOrWhiteSpace(this.EnteredCode);
        }

        private async Task VerifyCodeAsync()
        {
            if (string.IsNullOrWhiteSpace(this.enteredCode))
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.codeWordText,
                            Properties.Langs.Lang.requiredFieldsText, PopUpIcon.Warning);
                return;
            }
            this.isBusy = true;
            bool isCodeValid = await TryVerifyCodeAsync(this.enteredCode);
            if (!isCodeValid)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.verificationFailedTitleText,
                            Properties.Langs.Lang.codeIncorrectOrExpiredText, PopUpIcon.Error);
            }
            if (isCodeValid && this.reason == VerificationReason.AccountActivation)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.profileUpdatedTitleText,
                            Properties.Langs.Lang.successfulSignUpText, PopUpIcon.Success);
                this.windowService.ShowLoginWindow();
                this.windowService.CloseWindow(this);
            }
            if(isCodeValid && this.reason == VerificationReason.PasswordReset)
            {
                this.windowService.ShowChangePasswordWindow(this.Email, this.enteredCode);
                this.windowService.CloseWindow(this);
            }
            this.isBusy = false;
        }

        private async Task ResendCodeAsync()
        {
            this.IsBusy = true;
            bool success = await TryResendCodeAsync();
            if(success)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.codeSentTitleText,
                            Properties.Langs.Lang.newCodeSentText, PopUpIcon.Information);
            }
            else
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.timeLimitTitleText,
                            Properties.Langs.Lang.waitAMinuteForCodeText, PopUpIcon.Warning);
            }
            this.IsBusy = false;
        }

        private async Task<bool> TryVerifyCodeAsync(string code)
        {
            try
            {
                bool isValid = false;
                switch (this.reason)
                {
                    case VerificationReason.AccountActivation:
                        var dto = new EmailVerificationDTO { Email = this.Email, VerificationCode = code };
                        isValid = await newAccountClient.VerifyEmailAsync(dto);
                        return isValid;

                    case VerificationReason.PasswordReset:
                        var resetDto = new PasswordResetManagerServiceReference.EmailVerificationDTO { Email = this.Email, VerificationCode = code };
                        isValid = await resetPasswordClient.ValidatePasswordResetCodeAsync(resetDto);
                        return isValid;
                }
                return isValid;
            }
            catch (TimeoutException)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.timeLimitTitleText,
                            Properties.Langs.Lang.serverTimeoutText, PopUpIcon.Warning);
                return false;
            }
            catch (EndpointNotFoundException)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.connectionErrorTitleText,
                            Properties.Langs.Lang.serverConnectionInternetErrorText, PopUpIcon.Warning);
                return false;
            }
            catch (Exception)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                            Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
                return false;
            }
        }

        private async Task<bool> TryResendCodeAsync()
        {
            try
            {
                bool success = false;
                switch (reason)
                {
                    case VerificationReason.AccountActivation:
                        success = await newAccountClient.ResendVerificationCodeAsync(this.Email);
                        break;
                    case VerificationReason.PasswordReset:
                        var resetDto = new PasswordResetManagerServiceReference.EmailVerificationDTO { Email = this.Email, VerificationCode = "" };
                        success = await resetPasswordClient.RequestPasswordResetCodeAsync(resetDto);
                        break;
                }
                return success;
            }
            catch (TimeoutException)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.timeLimitTitleText,
                            Properties.Langs.Lang.serverTimeoutText, PopUpIcon.Warning);
                return false;
            }
            catch (EndpointNotFoundException)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.connectionErrorTitleText,
                            Properties.Langs.Lang.serverConnectionInternetErrorText, PopUpIcon.Warning);
                return false;
            }
            catch (Exception)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                            Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
                return false;
            }
        }
    }
}
