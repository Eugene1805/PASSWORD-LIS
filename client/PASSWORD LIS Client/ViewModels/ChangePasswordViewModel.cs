using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.PasswordResetManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class ChangePasswordViewModel : BaseViewModel
    {
        private readonly string email;
        private readonly string verificationCode;
        private readonly IPasswordResetManagerService passwordResetClient;

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

        public ChangePasswordViewModel(string email, string code, IWindowService windowService, 
            IPasswordResetManagerService passwordResetService) : base(windowService)
        {
            this.email = email;
            this.verificationCode = code;
            this.passwordResetClient = passwordResetService;
            this.ChangePasswordCommand = new RelayCommand(async (_) => await ChangePasswordAsync(), (_) => CanChangePassword());
        }

        private bool CanChangePassword()
        {
            return !this.IsBusy && !string.IsNullOrWhiteSpace(this.NewPassword) && 
                !string.IsNullOrWhiteSpace(this.ConfirmPassword);
        }

        private async Task ChangePasswordAsync()
        {
            if (!IsInputValid())
            {
                return;
            }

            this.IsBusy = true;
            try
            {
                await ExecuteAsync(async () =>
                {
                    bool success = await TryResetPasswordOnServerAsync();

                    if (success)
                    {
                        ProcessSuccessfulPasswordChange();
                    }
                    else
                    {
                        windowService.ShowPopUp(Properties.Langs.Lang.unexpectedErrorText,
                            Properties.Langs.Lang.passwordChangeFailedText, PopUpIcon.Warning);
                    }
                });
            }
            finally
            {
                this.IsBusy = false;
            }
        }

        private async Task<bool> TryResetPasswordOnServerAsync()
        {
            var passwordResetInfo = new PasswordResetDTO
            {
                NewPassword = this.NewPassword,
                Email = this.email,
                ResetCode = this.verificationCode
            };

            bool success = await passwordResetClient.ResetPasswordAsync(passwordResetInfo);
            return success;
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
