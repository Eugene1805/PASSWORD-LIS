using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.PasswordResetManagerServiceReference;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System;
using System.ServiceModel;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class ChangePasswordViewModel : BaseViewModel
    {
        private readonly string email;
        private readonly string verificationCode;
        private readonly IWindowService windowService;

        private string newPassword;
        public string NewPassword
        {
            get => this.newPassword;
            set { this.newPassword = value; OnPropertyChanged(); }
        }

        private string confirmPassword;
        public string ConfirmPassword
        {
            get => this.confirmPassword;
            set { this.confirmPassword = value; OnPropertyChanged(); }
        }

        private bool isBusy;
        public bool IsBusy
        {
            get => this.isBusy;
            set { this.isBusy = value; OnPropertyChanged(); }
        }

        public RelayCommand ChangePasswordCommand { get; }

        public ChangePasswordViewModel(string email, string code, IWindowService windowService)
        {
            this.email = email;
            this.verificationCode = code;
            this.windowService = windowService;
            this.ChangePasswordCommand = new RelayCommand(async (_) => await ChangePasswordAsync(), (_) => CanChangePassword());
        }
        public ChangePasswordViewModel() { }
        private bool CanChangePassword()
        {
            return !this.IsBusy && !string.IsNullOrWhiteSpace(this.NewPassword) && !string.IsNullOrWhiteSpace(this.ConfirmPassword);
        }

        private async Task ChangePasswordAsync()
        {
            if (!IsInputValid())
            {
                return;
            }

            this.isBusy = false;
            try
            {
                bool success = await TryResetPasswordOnServerAsync();

                if (success)
                {
                    ProcessSuccessfulPasswordChange();
                }
                else
                {
                    this.windowService.ShowPopUp(Properties.Langs.Lang.unexpectedErrorText,
                        Properties.Langs.Lang.passwordChangeFailedText, PopUpIcon.Warning);
                }
            }
            catch (Exception)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
            }
            finally
            {
                this.isBusy = true;
            }
        }

        private async Task<bool> TryResetPasswordOnServerAsync()
        {
            var client = new PasswordResetManagerClient();
            try
            {
                var passwordResetInfo = new PasswordResetDTO
                {
                    NewPassword = this.NewPassword,
                    Email = this.email,
                    ResetCode = this.verificationCode
                };

                bool success = await client.ResetPasswordAsync(passwordResetInfo);

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
        private void ProcessSuccessfulPasswordChange()
        {
            this.windowService.ShowPopUp(Properties.Langs.Lang.succesfulPasswordChangeTitleText,
                Properties.Langs.Lang.successfulPasswordChangeText, PopUpIcon.Success);
            this.windowService.ShowLoginWindow();
            this.windowService.CloseWindow(this);
        }
        private bool IsInputValid()
        {
            if (string.IsNullOrWhiteSpace(this.ConfirmPassword) || string.IsNullOrWhiteSpace(this.NewPassword))
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.warningTitleText,
                    Properties.Langs.Lang.requiredFieldsText, PopUpIcon.Warning);
                return false;
            }

            if (!ValidationUtils.PasswordsMatch(this.ConfirmPassword, this.NewPassword))
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.warningTitleText,
                    Properties.Langs.Lang.matchingPasswordErrorText, PopUpIcon.Warning);
                return false;
            }
            return true;
        }
    }
}
