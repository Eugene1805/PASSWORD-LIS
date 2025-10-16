﻿using PASSWORD_LIS_Client.Commands;
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
        public string Email { get; }
        private readonly VerificationReason reason;
        private readonly IWindowService windowService;

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

        public VerifyCodeViewModel(string email, VerificationReason reason, IWindowService windowService)
        {
            this.Email = email;
            this.reason = reason;
            this.windowService = windowService;

            this.VerifyCodeCommand = new RelayCommand(async (_) => await VerifyCodeAsync(), (_) => CanVerify());
            this.ResendCodeCommand = new RelayCommand(async (_) => await ResendCodeAsync(), (_) => !this.IsBusy);
        }
        public VerifyCodeViewModel() { }
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
                        var activationClient = new AccountVerificationManagerClient();
                        var dto = new EmailVerificationDTO { Email = this.Email, VerificationCode = code };
                        isValid = await activationClient.VerifyEmailAsync(dto);
                        activationClient.Close();
                        return isValid;

                    case VerificationReason.PasswordReset:
                        var resetClient = new PasswordResetManagerServiceReference.PasswordResetManagerClient();
                        var resetDto = new PasswordResetManagerServiceReference.EmailVerificationDTO { Email = this.Email, VerificationCode = code };
                        isValid = await resetClient.ValidatePasswordResetCodeAsync(resetDto);
                        resetClient.Close();
                        Console.WriteLine(isValid);
                        Console.WriteLine(resetDto.Email);

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
                        var activationClient = new AccountVerificationManagerClient();
                        success = await activationClient.ResendVerificationCodeAsync(this.Email);
                        activationClient.Close();
                        break;
                    case VerificationReason.PasswordReset:
                        var resetClient = new PasswordResetManagerServiceReference.PasswordResetManagerClient();
                        var resetDto = new PasswordResetManagerServiceReference.EmailVerificationDTO { Email = this.Email, VerificationCode = "" };
                        success = await resetClient.RequestPasswordResetCodeAsync(resetDto);
                        resetClient.Close();
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
