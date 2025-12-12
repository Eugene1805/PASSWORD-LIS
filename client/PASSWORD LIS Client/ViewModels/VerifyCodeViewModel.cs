using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.VerificationCodeManagerServiceReference;
using PASSWORD_LIS_Client.Views;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class VerifyCodeViewModel : BaseViewModel
    {
        private readonly VerificationReason reason;
        private readonly IVerificationCodeManagerService newAccountClient;
        private readonly IPasswordResetManagerService resetPasswordClient;
        private const int CodeLength = 6;
        public string Email 
        { 
            get; 
        }
        private string enteredCode;
        public string EnteredCode
        {
            get => this.enteredCode;
            set
            {
                this.enteredCode = value;
                ValidateEnteredCode();
                OnPropertyChanged();
            }
        }

        private bool isBusy;
        public bool IsBusy
        {
            get => this.isBusy;
            set
            {
                this.isBusy = value;
                OnPropertyChanged();
            }
        }

        private string enteredCodeError;
        public string EnteredCodeError
        {
            get => enteredCodeError;
            set 
            { 
                enteredCodeError = value; 
                OnPropertyChanged(); 
            }
        }

        public RelayCommand VerifyCodeCommand 
        { 
            get; 
        }
        public RelayCommand ResendCodeCommand 
        { 
            get; 
        }

        public VerifyCodeViewModel(string Email, VerificationReason Reason, IWindowService WindowService,
            IVerificationCodeManagerService VerificationCodeManager, IPasswordResetManagerService PasswordResetManager)
            : base(WindowService)
        {
            this.Email = Email;
            this.reason = Reason;
            this.newAccountClient = VerificationCodeManager;
            this.resetPasswordClient = PasswordResetManager;

            this.VerifyCodeCommand = new RelayCommand(async (_) => await VerifyCodeAsync(), (_) => CanVerify());
            this.ResendCodeCommand = new RelayCommand(async (_) => await ResendCodeAsync(), (_) => !this.IsBusy);
        }

        private bool CanVerify()
        {
            return !this.IsBusy && !string.IsNullOrWhiteSpace(this.EnteredCode) && ValidateEnteredCode();
        }

        private async Task VerifyCodeAsync()
        {
            if (!ValidateEnteredCode())
            {
                return;
            }
            this.IsBusy = true;
            try
            {
                bool isCodeValid = await TryVerifyCodeAsync(this.enteredCode);

                if (!isCodeValid)
                {
                    EnteredCodeError = Properties.Langs.Lang.codeIncorrectOrExpiredText;
                }
                if (isCodeValid && this.reason == VerificationReason.AccountActivation)
                {
                    this.windowService.ShowPopUp(Properties.Langs.Lang.profileUpdatedTitleText,
                                Properties.Langs.Lang.successfulSignUpText, PopUpIcon.Success);
                    this.windowService.ShowLoginWindow();
                    this.windowService.CloseWindow(this);
                    windowService.CloseMainWindow();
                }
                if (isCodeValid && this.reason == VerificationReason.PasswordReset)
                {
                    this.windowService.ShowChangePasswordWindow(this.Email, this.enteredCode);
                    this.windowService.CloseWindow(this);
                    windowService.CloseMainWindow();
                }
            }
            finally
            {
                this.IsBusy = false;
            }
        }

        private async Task ResendCodeAsync()
        {
            this.IsBusy = true;
            try
            {
                bool success = await TryResendCodeAsync();
                if (success)
                {
                    this.windowService.ShowPopUp(Properties.Langs.Lang.codeSentTitleText,
                                Properties.Langs.Lang.newCodeSentText, PopUpIcon.Information);
                }
                else
                {
                    this.windowService.ShowPopUp(Properties.Langs.Lang.timeLimitTitleText,
                                Properties.Langs.Lang.waitAMinuteForCodeText, PopUpIcon.Warning);
                }
            }
            finally
            {
                this.IsBusy = false;
            }
        }

        private async Task<bool> TryVerifyCodeAsync(string code)
        {
            return await ExecuteAsync(async () =>
            {
                bool isValid = false;
                switch (this.reason)
                {
                    case VerificationReason.AccountActivation:
                        var dto = new EmailVerificationDTO 
                        { 
                            Email = this.Email, 
                            VerificationCode = code 
                        };
                        isValid = await newAccountClient.VerifyEmailAsync(dto);
                        return isValid;

                    case VerificationReason.PasswordReset:
                        var resetDto = new PasswordResetManagerServiceReference.EmailVerificationDTO
                        { 
                            Email = this.Email, 
                            VerificationCode = code 
                        };
                        isValid = await resetPasswordClient.ValidatePasswordResetCodeAsync(resetDto);
                        return isValid;
                }
                return isValid;
            });
        }

        private async Task<bool> TryResendCodeAsync()
        {
            return await ExecuteAsync(async () =>
            {
                bool success = false;
                switch (reason)
                {
                    case VerificationReason.AccountActivation:
                        success = await newAccountClient.ResendVerificationCodeAsync(this.Email);
                        break;
                    case VerificationReason.PasswordReset:
                        var resetDto = new PasswordResetManagerServiceReference.EmailVerificationDTO
                        { 
                            Email = this.Email, 
                            VerificationCode = string.Empty 
                        };
                        success = await resetPasswordClient.RequestPasswordResetCodeAsync(resetDto);
                        break;
                }
                return success;
            });
        }

        private bool ValidateEnteredCode()
        {
            if (string.IsNullOrWhiteSpace(EnteredCode))
            {
                EnteredCodeError = Properties.Langs.Lang.requiredFieldsText;
                return false;
            }
            if (EnteredCode.Length != CodeLength)
            {
                EnteredCodeError = Properties.Langs.Lang.codeIncorrectOrExpiredText;
                return false;
            }
            EnteredCodeError = null;
            return true;
        }
    }
}
