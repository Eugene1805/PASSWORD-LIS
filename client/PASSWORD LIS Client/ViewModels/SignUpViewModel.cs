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
using PASSWORD_LIS_Client.Services;

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
        

        public ICommand SignUpCommand { get; }
        public ICommand NavigateToLoginCommand { get; }
        public ICommand OpenTermsAndConditionsCommand { get; }
        private readonly IAccountManagerService client;
        
        private readonly IWindowService windowService;
        public SignUpViewModel(IAccountManagerService accountManager,IWindowService windowService)
        {
            this.client = accountManager;
            this.windowService = windowService;
            TCLink = ConfigurationManager.AppSettings["TCPageURL"];
            SignUpCommand = new RelayCommand(async (_) => await SignUpAsync(), (_) => CanExecuteSignUp());
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
                this.windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.userAlreadyExistText, PopUpIcon.Warning);
            }
            catch (TimeoutException)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.timeLimitTitleText,
                    Properties.Langs.Lang.serverTimeoutText, PopUpIcon.Warning);
            }
            catch (EndpointNotFoundException)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.connectionErrorTitleText,
                    Properties.Langs.Lang.serverConnectionInternetErrorText, PopUpIcon.Error);
            }
            catch (CommunicationException)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.networkErrorTitleText,
                    Properties.Langs.Lang.serverCommunicationErrorText, PopUpIcon.Error);
            }
            catch (Exception)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
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

        private void NavigateToLogin(object obj)
        {
            this.windowService.ShowLoginWindow();
            this.windowService.CloseWindow(this);
        }

        private void OpenTermsAndConditions(object obj)
        {
            if (string.IsNullOrWhiteSpace(TCLink) || !Uri.IsWellFormedUriString(TCLink, UriKind.Absolute))
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.invalidLinkTitle,
                    Properties.Langs.Lang.linkUnavailableText, PopUpIcon.Warning);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(TCLink) { UseShellExecute = true });
            }
            catch (Win32Exception)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.operationFailedTitleText,
                    Properties.Langs.Lang.linkOpenFailText, PopUpIcon.Warning);
            }
            catch (ArgumentNullException)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.internalErrorTitle,
                    Properties.Langs.Lang.termsLinkUnavailableText, PopUpIcon.Error);
            }
            catch (Exception)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.unknownErrorTitle, 
                    Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
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
            await client.CreateAccountAsync(userAccount);
        }

        private void ProcessSuccessfulSignUp()
        {
            this.windowService.ShowVerifyCodeWindow(this.Email, VerificationReason.AccountActivation);
            this.windowService.CloseWindow(this);
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
