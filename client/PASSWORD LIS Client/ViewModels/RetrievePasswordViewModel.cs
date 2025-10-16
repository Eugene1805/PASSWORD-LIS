using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.PasswordResetManagerServiceReference;
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
        public RetrievePasswordViewModel()
        {
            this.windowService = new WindowService();
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
            }
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
            var client = new PasswordResetManagerClient();
            try
            {
                var requestDto = new EmailVerificationDTO { Email = email, VerificationCode = "" };
                bool success = await client.RequestPasswordResetCodeAsync(requestDto);
                client.Close();
                return success;
            }
            catch (TimeoutException)
            {
                client.Abort();
                this.windowService.ShowPopUp(Properties.Langs.Lang.timeLimitTitleText,
                    Properties.Langs.Lang.serverTimeoutText, PopUpIcon.Warning);
                return false;
            }
            catch (EndpointNotFoundException)
            {
                client.Abort();
                this.windowService.ShowPopUp(Properties.Langs.Lang.connectionErrorTitleText,
                    Properties.Langs.Lang.serverConnectionInternetErrorText, PopUpIcon.Warning);
                return false;
            }
            catch (CommunicationException)
            {
                client.Abort();
                this.windowService.ShowPopUp(Properties.Langs.Lang.networkErrorTitleText,
                    Properties.Langs.Lang.serverCommunicationErrorText, PopUpIcon.Warning);
                return false;
            }
            catch (Exception)
            {
                client.Abort();
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
