using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.PasswordResetManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class RetrievePasswordViewModel : BaseViewModel
    {
        private readonly IPasswordResetManagerService passwordResetClient;

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

        public RetrievePasswordViewModel(IPasswordResetManagerService resetManagerService, IWindowService windowService) 
            : base(windowService)
        {
            this.passwordResetClient = resetManagerService;
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
                await ExecuteAsync(async () =>
                {
                    bool success = await TryRequestResetCodeAsync(this.email);

                    if (success)
                    {
                        ProcessCodeRequestSuccess(this.email);
                    }
                    else
                    {
                        windowService.ShowPopUp(Properties.Langs.Lang.sendFailedTitleText,
                            Properties.Langs.Lang.codeSendFailedText, PopUpIcon.Error);
                    }
                });
            }
            finally
            {
                this.IsBusy = false;
            }
        }

        private async Task<bool> TryRequestResetCodeAsync(string email)
        {
            var requestDto = new EmailVerificationDTO { Email = email, VerificationCode = "" };
            bool success = await passwordResetClient.RequestPasswordResetCodeAsync(requestDto);
            return success;
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
