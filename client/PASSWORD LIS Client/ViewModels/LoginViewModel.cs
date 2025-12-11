using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.LoginManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        private readonly ILoginManagerService loginManagerService;

        private const int MaximumEmailLength = 100;
        private const int MaximumPasswordLength = 15;
        private const int MinimumGuestRandomNumber = 1000;
        private const int MaximumGuestRandomNumber = 9999;
        private const int GuestPlayerId = -1;
        private const int DefaultPhotoId = 0;
        private const int MinimumValidUserId = 0;

        private string email;
        public string Email
        {
            get => email;
            set 
            { 
                email = value;
                ValidateEmail();
                OnPropertyChanged(); 
            }
        }

        private string password;
        public string Password
        {
            get => password;
            set 
            { 
                password = value;
                ValidatePassword();
                OnPropertyChanged(); 
            }
        }

        private bool isLoggingIn;
        public bool IsLoggingIn
        {
            get => isLoggingIn;
            set { isLoggingIn = value; OnPropertyChanged(); }
        }

        private string emailError;
        public string EmailError
        {
            get => emailError;
            set { emailError = value; OnPropertyChanged(); }
        }

        private string passwordError;
        public string PasswordError
        {
            get => passwordError;
            set { passwordError = value; OnPropertyChanged(); }
        }

        public ICommand LoginCommand { get; }
        public ICommand PlayAsGuestCommand { get; }
        public ICommand NavigateToSignUpCommand { get; }
        public ICommand NavigateToForgotPasswordCommand { get; }

        public LoginViewModel(ILoginManagerService LoginManagerService, IWindowService WindowService) 
            : base(WindowService)
        {
            this.loginManagerService = LoginManagerService;

            LoginCommand = new RelayCommand(async (_) => await LoginAsync(), (_) => CanLogin());
            PlayAsGuestCommand = new RelayCommand(PlayAsGuest);
            NavigateToSignUpCommand = new RelayCommand(NavigateToSignUp);
            NavigateToForgotPasswordCommand = new RelayCommand(NavigateToForgotPassword);
        }

        private bool CanLogin()
        {
            return !IsLoggingIn;
        }

        private async Task LoginAsync()
        {
            if (!AreFieldsValid())
            {
                return;
            }

            IsLoggingIn = true;
            try
            {
                await ExecuteAsync(ProcessLoginSequenceAsync);
            }
            finally
            {
                IsLoggingIn = false;
            }
        }

        private async Task ProcessLoginSequenceAsync()
        {
            var loggedInUser = await loginManagerService.LoginAsync(Email, Password);
            bool isAccountVerified = false;

            if (loggedInUser.UserAccountId > MinimumValidUserId)
            {
                isAccountVerified = await loginManagerService.IsAccountVerifiedAsync(Email);
            }

            if (loggedInUser.UserAccountId > MinimumValidUserId && isAccountVerified)
            {
                ProcessSuccessfulLogin(loggedInUser);
            }
            else if (loggedInUser.UserAccountId > MinimumValidUserId && !isAccountVerified)
            {
                await loginManagerService.SendVerificationCodeAsync(loggedInUser.Email);
                VerifyAccount(loggedInUser.Email);
            }
            else
            {
                windowService.ShowPopUp(Properties.Langs.Lang.warningTitleText,
                    Properties.Langs.Lang.wrongCredentialsText, PopUpIcon.Warning);
            }
        }
        private void ProcessSuccessfulLogin(UserDTO loggedInUser)
        {
            SessionManager.Login(loggedInUser);
            windowService.ShowPopUp(Properties.Langs.Lang.successfulLoginText,
                        string.Format(Properties.Langs.Lang.loginWelcomeText, SessionManager.CurrentUser.Nickname),
                        PopUpIcon.Success);

            windowService.ShowMainWindow();
            windowService.CloseWindow(this);
        }

        private void PlayAsGuest(object parameter)
        {
            string guestNickname = Properties.Langs.Lang.guestText + new Random().Next(MinimumGuestRandomNumber, MaximumGuestRandomNumber);
            var guestUser = new UserDTO
            {
                PlayerId = GuestPlayerId,
                Nickname = guestNickname,
                FirstName = Properties.Langs.Lang.guestText,
                LastName = string.Empty,
                PhotoId = DefaultPhotoId,
                SocialAccounts = new Dictionary<string, string>()
            };

            SessionManager.Login(guestUser);

            windowService.ShowPopUp(
                Properties.Langs.Lang.successfulLoginText,
                string.Format(Properties.Langs.Lang.loginWelcomeText, guestUser.Nickname),
                PopUpIcon.Success);

            windowService.ShowMainWindow();
            windowService.CloseWindow(this);
        }

        private void NavigateToSignUp(object parameter)
        {
            var signUpViewModel = new SignUpViewModel(App.AccountManagerService, windowService); 
            var signUpWindow = new SignUpWindow { DataContext = signUpViewModel };
            signUpWindow.Show();
            windowService.CloseWindow(this);
            windowService.CloseMainWindow();
        }

        private void NavigateToForgotPassword(object parameter)
        {
            var retrivePasswordViewModel = new RetrievePasswordViewModel(App.PasswordResetManagerService, windowService);
            var retrievePasswordWindow = new RetrievePasswordWindow { DataContext = retrivePasswordViewModel};
            retrievePasswordWindow.ShowDialog();
            windowService.CloseWindow(this);
        }

        private void VerifyAccount(string email)
        {
            windowService.ShowVerifyCodeWindow(email, VerificationReason.AccountActivation);
            windowService.CloseWindow(this);
            windowService.CloseMainWindow();
        }

        private bool AreFieldsValid()
        {
            bool isEmailValid = ValidateEmail();
            bool isPasswordValid = ValidatePassword();

            return isEmailValid && isPasswordValid;
        }

        private bool ValidateEmail()
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                EmailError = Properties.Langs.Lang.emptyEmailText;
                return false;
            }

            if (Email.Length > MaximumEmailLength)
            {
                EmailError = Properties.Langs.Lang.emailTooLongText;
                return false;
            }

            EmailError = null;
            return true;
        }

        private bool ValidatePassword()
        {
            if (string.IsNullOrWhiteSpace(Password))
            {
                PasswordError = Properties.Langs.Lang.emptyPasswordText;
                return false;
            }

            if (Password.Length > MaximumPasswordLength)
            {
                PasswordError = Properties.Langs.Lang.passwordTooLongText;
                return false;
            }

            PasswordError = null;
            return true;
        }
    }
}