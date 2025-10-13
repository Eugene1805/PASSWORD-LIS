using PASSWORD_LIS_Client.AccountManagerServiceReference;
using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.Views;
using PASSWORD_LIS_Client.Utils;
using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class SignUpViewModel : BaseViewModel
    {
        private string firstName;
        public string FirstName
        {
            get => firstName;
            set { firstName = value; OnPropertyChanged(); }
        }

        private string lastName;
        public string LastName
        {
            get => lastName;
            set { lastName = value; OnPropertyChanged(); }
        }

        private string nickname;
        public string Nickname
        {
            get => nickname;
            set { nickname = value; OnPropertyChanged(); }
        }

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

        private string confirmPassword;
        public string ConfirmPassword
        {
            get => confirmPassword;
            set { confirmPassword = value; OnPropertyChanged(); }
        }

        private bool isSigningUp;
        public bool IsSigningUp
        {
            get => isSigningUp;
            set { isSigningUp = value; OnPropertyChanged(); }
        }

        public string TCLink { get; }
        
        private Window popUpWindow = null;

        public ICommand SignUpCommand { get; }
        public ICommand NavigateToLoginCommand { get; }
        public ICommand OpenTermsAndConditionsCommand { get; }
        

        public SignUpViewModel()
        {
            TCLink = ConfigurationManager.AppSettings["TCPageURL"];
            SignUpCommand = new RelayCommand((_) => SignUpAsync(), _ => CanExecuteSignUp());
            NavigateToLoginCommand = new RelayCommand(NavigateToLogin);
            OpenTermsAndConditionsCommand = new RelayCommand(OpenTermsAndConditions);
        }

        private async Task SignUpAsync()
        {

            if (!IsInputValid())
            {
                return;
            }

            IsSigningUp = true;
            try
            {
                await TryCreateAccountOnServerAsync();
                ProcessSuccessfulSignUp();
            }
            catch (FaultException<ServiceErrorDetailDTO>)
            {
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.userAlreadyExistText,PopUpIcon.Warning);
                popUpWindow.ShowDialog();
            }
            catch (TimeoutException)
            {
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.timeLimitTitleText,
                    Properties.Langs.Lang.serverTimeoutText,PopUpIcon.Warning);
                popUpWindow.ShowDialog();
            }
            catch (EndpointNotFoundException)
            {
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.connectionErrorTitleText,
                    Properties.Langs.Lang.serverConnectionInternetErrorText,PopUpIcon.Error);
                popUpWindow.ShowDialog();
            }
            catch (CommunicationException)
            {
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.networkErrorTitleText,
                    Properties.Langs.Lang.serverCommunicationErrorText,PopUpIcon.Error);
                popUpWindow.ShowDialog();
            }
            catch (Exception)
            {
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.errorTitleText,
                                    Properties.Langs.Lang.unexpectedErrorText,PopUpIcon.Error);
                popUpWindow.ShowDialog();
            }
            finally
            {
                IsSigningUp = false;
            }
        }

        private bool CanExecuteSignUp()
        {

            return !IsSigningUp &&
                   !string.IsNullOrEmpty(Email) &&
                   !string.IsNullOrEmpty(FirstName) &&
                   !string.IsNullOrEmpty(LastName) &&
                   !string.IsNullOrEmpty(Nickname) &&
                   !string.IsNullOrEmpty(Password) &&
                   !string.IsNullOrEmpty(ConfirmPassword);
        }

        private static void NavigateToLogin(object obj)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();

            foreach (Window window in Application.Current.Windows)
            {
                if (window is SignUpWindow)
                {
                    window.Close();
                    break;
                }
            }
        }

        private void OpenTermsAndConditions(object obj)
        {
            if (string.IsNullOrWhiteSpace(TCLink) || !Uri.IsWellFormedUriString(TCLink, UriKind.Absolute))
            {
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.invalidLinkTitle,
                    Properties.Langs.Lang.linkUnavailableText,PopUpIcon.Warning);
                popUpWindow.ShowDialog();
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(TCLink) { UseShellExecute = true });
            }
            catch (Win32Exception)
            {
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.operationFailedTitleText,
                    Properties.Langs.Lang.linkOpenFailText, PopUpIcon.Warning);
                popUpWindow.ShowDialog();
            }
            catch (ArgumentNullException)
            {
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.internalErrorTitle,
                    Properties.Langs.Lang.termsLinkUnavailableText,PopUpIcon.Error);
            }
            catch (Exception)
            {
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.unknownErrorTitle,
                    Properties.Langs.Lang.unexpectedErrorText,PopUpIcon.Error);
                popUpWindow.ShowDialog();
            }
        }

        private async Task TryCreateAccountOnServerAsync()
        {
            var userAccount = new NewAccountDTO
            {
                Email = this.Email,
                Password = this.Password,
                FirstName = this.FirstName,
                LastName = this.LastName,
                Nickname = this.Nickname
            };

            var client = new AccountManagerClient();
            try
            {
                await client.CreateAccountAsync(userAccount);
                client.Close();
            }
            catch(Exception)
            {
                client.Abort();
                throw;
            }
        }

        private void ProcessSuccessfulSignUp()
        {
            var codeVerificationWindow = new VerifyCodeWindow(Email, VerificationReason.AccountActivation);
            bool? result = codeVerificationWindow.ShowDialog();

            if (result == true)
            {
                NavigateToLogin(null);
            }
        }

        private bool IsInputValid()
        {
            if (!ValidationUtils.IsValidEmail(Email))
            {
                MessageBox.Show(Properties.Langs.Lang.invalidEmailErrorText);
                return false;
            }
            if (!ValidationUtils.PasswordsMatch(Password, ConfirmPassword))
            {
                MessageBox.Show(Properties.Langs.Lang.matchingPasswordErrorText);
                return false;
            }
            if (!ValidationUtils.ArePasswordRequirementsMet(Password))
            {
                MessageBox.Show(Properties.Langs.Lang.userPasswordRequirementsText);
                return false;
            }
            return true;
        }
             
        
    }
}
