using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.LoginManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        private string email;
        public string Email
        {
            get => email;
            set { email = value; OnPropertyChanged(); }
        }

        private string password;
        public string Password
        {
            get => password;
            set { password = value; OnPropertyChanged(); }
        }

        private bool isLoggingIn;
        public bool IsLoggingIn
        {
            get => isLoggingIn;
            set { isLoggingIn = value; OnPropertyChanged(); }
        }

        public ICommand LoginCommand { get; }
        public ICommand PlayAsGuestCommand { get; }
        public ICommand NavigateToSignUpCommand { get; }
        public ICommand NavigateToForgotPasswordCommand { get; }

        private readonly ILoginManagerService loginManagerService;

        public LoginViewModel(ILoginManagerService loginManagerService, IWindowService windowService) 
            : base(windowService)
        {
            this.loginManagerService = loginManagerService;

            LoginCommand = new RelayCommand(async (_) => await LoginAsync(), (_) => CanLogin());
            PlayAsGuestCommand = new RelayCommand(PlayAsGuest);
            NavigateToSignUpCommand = new RelayCommand(NavigateToSignUp);
            NavigateToForgotPasswordCommand = new RelayCommand(NavigateToForgotPassword);
        }

        private bool CanLogin()
        {
            return !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password) && !IsLoggingIn;
        }

        private async Task LoginAsync()
        {
            IsLoggingIn = true;
            try
            {
                await ExecuteAsync(async () =>
                {
                    var loggedInUser = await loginManagerService.LoginAsync(Email, Password);
                    bool isAccountVerified = false;

                    if (loggedInUser.UserAccountId > 0) 
                    {
                        isAccountVerified = await loginManagerService.IsAccountVerifiedAsync(Email);
                    }

                    if (loggedInUser.UserAccountId > 0 && isAccountVerified)
                    {
                        ProcessSuccessfulLogin(loggedInUser);
                    }
                    else if (loggedInUser.UserAccountId > 0 && !isAccountVerified)
                    {
                        await loginManagerService.SendVerificationCodeAsync(loggedInUser.Email);
                        VerifyAccount(loggedInUser.Email);
                    }
                    else
                    {
                        windowService.ShowPopUp(Properties.Langs.Lang.warningTitleText,
                            Properties.Langs.Lang.wrongCredentialsText, PopUpIcon.Warning);
                    }
                });
            }
            finally
            {
                IsLoggingIn = false;
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
            string guestNickname = Properties.Langs.Lang.guestText + new Random().Next(1000, 9999);
            var guestUser = new UserDTO
            {
                PlayerId = -1,
                Nickname = guestNickname,
                FirstName = Properties.Langs.Lang.guestText,
                LastName = "",
                PhotoId = 0,
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
            var retreivePasswordViewModel = new RetrievePasswordViewModel(App.PasswordResetManagerService, windowService);
            var retrievePasswordWindow = new RetrievePasswordWindow { DataContext = retreivePasswordViewModel};
            retrievePasswordWindow.ShowDialog();
            windowService.CloseWindow(this);
        }

        private void VerifyAccount(string email)
        {
            windowService.ShowVerifyCodeWindow(email, VerificationReason.AccountActivation);
            windowService.CloseWindow(this);
            windowService.CloseMainWindow();
        }
    }
}