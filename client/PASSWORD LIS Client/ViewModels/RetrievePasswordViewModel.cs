using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.PasswordResetManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System;
using System.ServiceModel;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class RetrievePasswordViewModel : BaseViewModel
    {
        private string email;
        public string Email
        {
            get => this.email;
            set { this.email = value; OnPropertyChanged(); }
        }

        private bool isBusy;
        public bool IsBusy
        {
            get => this.isBusy;
            set { this.isBusy = value; OnPropertyChanged(); }
        }

        public RelayCommand SendCodeCommand { get; }

        private readonly IWindowService windowService;
        private readonly IPasswordResetManagerService passwordResetClient;
        public RetrievePasswordViewModel(IPasswordResetManagerService resetManagerService,IWindowService windowService)
        {
            this.passwordResetClient = resetManagerService;
            this.windowService = windowService;
            this.SendCodeCommand = new RelayCommand(async (_) => await SendCodeAsync(), (_) => CanSendCode());
        }

        private bool CanSendCode()
        {
            return !this.IsBusy && !string.IsNullOrWhiteSpace(this.Email);
        }

        private async Task SendCodeAsync()
        {
            if (!IsInputValid())
            {
                return;
            }
            this.IsBusy = true;
            try
            {
                bool success = await TryRequestResetCodeAsync(this.email);

                if (success)
                {
                    ProcessCodeRequestSuccess(this.email);
                }
                else
                {
                    this.windowService.ShowPopUp(Properties.Langs.Lang.sendFailedTitleText,
                        Properties.Langs.Lang.codeSendFailedText, PopUpIcon.Error);
                }
            }// TODO: Add excetion handling
            catch (Exception)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
            }
            finally
            {
                this.IsBusy = false;
            }
        }

        private async Task<bool> TryRequestResetCodeAsync(string email)
        {
            try
            {
                var requestDto = new EmailVerificationDTO { Email = email, VerificationCode = "" };
                bool success = await passwordResetClient.RequestPasswordResetCodeAsync(requestDto);
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
            catch (CommunicationException)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.networkErrorTitleText,
                    Properties.Langs.Lang.serverCommunicationErrorText, PopUpIcon.Warning);
                return false;
            }
            catch (Exception)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
                return false;
            }
        }

        private void ProcessCodeRequestSuccess(string userEmail)
        {
            this.windowService.ShowVerifyCodeWindow(userEmail, VerificationReason.PasswordReset);
            this.windowService.CloseWindow(this);
        }

        private bool IsInputValid()
        {
            if (string.IsNullOrWhiteSpace(this.email))
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.warningTitleText,
                    Properties.Langs.Lang.requiredFieldsText, PopUpIcon.Warning);
                return false;
            }

            if (!ValidationUtils.IsValidEmail(email))
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.warningTitleText,
                    Properties.Langs.Lang.invalidEmailErrorText, PopUpIcon.Warning);
                return false;
            }
            return true;
        }

    }
}
