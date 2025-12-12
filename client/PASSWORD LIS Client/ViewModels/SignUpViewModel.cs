using PASSWORD_LIS_Client.AccountManagerServiceReference;
using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.Views;
using PASSWORD_LIS_Client.Utils;
using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using PASSWORD_LIS_Client.Services;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class SignUpViewModel : BaseViewModel
    {
        private readonly IAccountManagerService client;
        private const int MaximumFirstNameLength = 50;
        private const int MaximumLastNameLength = 80;
        private const int MaximumNicknameLength = 50;
        private const int MaximumEmailLength = 100;

        private string firstName;
        public string FirstName
        {
            get => firstName;
            set 
            { 
                firstName = value; ValidateFirstName(); 
                OnPropertyChanged(); 
            }
        }

        private string lastName;
        public string LastName
        {
            get => lastName;
            set 
            { 
                lastName = value; 
                ValidateLastName(); 
                OnPropertyChanged(); 
            }
        }

        private string nickname;
        public string Nickname
        {
            get => nickname;
            set 
            { 
                nickname = value; 
                ValidateNickname(); 
                OnPropertyChanged(); 
            }
        }

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

        private string confirmPassword;
        public string ConfirmPassword
        {
            get => confirmPassword;
            set 
            { 
                confirmPassword = value; 
                ValidateConfirmPassword(); 
                OnPropertyChanged(); 
            }
        }

        private bool isSigningUp;
        public bool IsSigningUp
        {
            get => isSigningUp;
            set 
            { 
                isSigningUp = value; 
                OnPropertyChanged(); 
            }
        }

        private string firstNameError;
        public string FirstNameError
        {
            get => firstNameError;
            set 
            { 
                firstNameError = value; 
                OnPropertyChanged(); 
            }
        }

        private string lastNameError;
        public string LastNameError
        {
            get => lastNameError;
            set 
            { 
                lastNameError = value; 
                OnPropertyChanged(); 
            }
        }

        private string nicknameError;
        public string NicknameError
        {
            get => nicknameError;
            set 
            { 
                nicknameError = value; 
                OnPropertyChanged(); 
            }
        }

        private string emailError;
        public string EmailError
        {
            get => emailError;
            set 
            { 
                emailError = value; 
                OnPropertyChanged(); 
            }
        }

        private string passwordError;
        public string PasswordError
        {
            get => passwordError;
            set 
            { 
                passwordError = value; 
                OnPropertyChanged(); 
            }
        }

        private string confirmPasswordError;
        public string ConfirmPasswordError
        {
            get => confirmPasswordError;
            set 
            { 
                confirmPasswordError = value; 
                OnPropertyChanged(); 
            }
        }

        public string TCLink 
        { 
            get; 
        }

        public ICommand SignUpCommand 
        { 
            get; 
        }
        public ICommand NavigateToLoginCommand 
        { 
            get; 
        }
        public ICommand OpenTermsAndConditionsCommand 
        { 
            get; 
        }

        public SignUpViewModel(IAccountManagerService AccountManager, IWindowService WindowService) 
            : base(WindowService)
        {
            this.client = AccountManager;
            TCLink = ConfigurationManager.AppSettings["TCPageURL"];
            SignUpCommand = new RelayCommand(async (_) => await SignUpAsync(), (_) => CanExecuteSignUp());
            NavigateToLoginCommand = new RelayCommand(NavigateToLogin);
            OpenTermsAndConditionsCommand = new RelayCommand(OpenTermsAndConditions);
        }

        public async Task SignUpAsync()
        {
            if (!await IsInputValid())
            {
                return;
            }
            IsSigningUp = true;
            try
            {
                await ExecuteAsync(async () =>
                {
                    await TryCreateAccountOnServerAsync();
                    ProcessSuccessfulSignUp();
                });
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
                   !string.IsNullOrEmpty(ConfirmPassword) &&
                   AreFieldsValid();
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

        private async Task<bool> IsInputValid()
        {
            bool isValid = AreFieldsValid();

            if (!isValid)
            {
                return false;
            }

            try
            {
                if (await client.IsNicknameInUseAsync(nickname))
                {
                    NicknameError = Properties.Langs.Lang.nicknameInUseText;
                    return false;
                }
            }
            catch
            {
                windowService.ShowPopUp(Properties.Langs.Lang.warningTitleText,
                    Properties.Langs.Lang.networkErrorTitleText, PopUpIcon.Error);
                return false;
            }

            return true;
        }

        private bool AreFieldsValid()
        {
            bool isFirstNameValid = ValidateFirstName();
            bool isLastNameValid = ValidateLastName();
            bool isNicknameValid = ValidateNickname();
            bool isEmailValid = ValidateEmail();
            bool isPasswordValid = ValidatePassword();
            bool isConfirmPasswordValid = ValidateConfirmPassword();

            return isFirstNameValid && isLastNameValid && isNicknameValid && 
                   isEmailValid && isPasswordValid && isConfirmPasswordValid;
        }

        private bool ValidateFirstName()
        {
            if (string.IsNullOrWhiteSpace(FirstName))
            {
                FirstNameError = Properties.Langs.Lang.emptyFirstNameText;
                return false;
            }
            if (FirstName.Length > MaximumFirstNameLength)
            {
                FirstNameError = Properties.Langs.Lang.firstNameTooLongText;
                return false;
            }
            if (!ValidationUtils.ContainsOnlyLetters(FirstName))
            {
                FirstNameError = Properties.Langs.Lang.nameInvalidCharsText;
                return false;
            }
            FirstNameError = null;
            return true;
        }

        private bool ValidateLastName()
        {
            if (string.IsNullOrWhiteSpace(LastName))
            {
                LastNameError = Properties.Langs.Lang.emptyLastNameText;
                return false;
            }
            if (LastName.Length > MaximumLastNameLength)
            {
                LastNameError = Properties.Langs.Lang.lastNameTooLongText;
                return false;
            }
            if (!ValidationUtils.ContainsOnlyLetters(LastName))
            {
                LastNameError = Properties.Langs.Lang.lastNameInvalidCharsText;
                return false;
            }
            LastNameError = null;
            return true;
        }

        private bool ValidateNickname()
        {
            if (string.IsNullOrWhiteSpace(Nickname))
            {
                NicknameError = Properties.Langs.Lang.emptyNicknameText;
                return false;
            }
            if (Nickname.Length > MaximumNicknameLength)
            {
                NicknameError = Properties.Langs.Lang.nicknameTooLongText;
                return false;
            }
            NicknameError = null;
            return true;
        }

        private bool ValidateEmail()
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                EmailError = Properties.Langs.Lang.emptyFirstNameText;
                return false;
            }
            if (Email.Length > MaximumEmailLength)
            {
                EmailError = Properties.Langs.Lang.emailTooLongText;
                return false;
            }
            if (!ValidationUtils.IsValidEmail(Email))
            {
                EmailError = Properties.Langs.Lang.invalidEmailFormatText;
                return false;
            }
            EmailError = null;
            return true;
        }

        private bool ValidatePassword()
        {
            if (string.IsNullOrWhiteSpace(Password))
            {
                PasswordError = Properties.Langs.Lang.userPasswordRequirementsText;
                return false;
            }
            if (!ValidationUtils.ArePasswordRequirementsMet(Password))
            {
                PasswordError = Properties.Langs.Lang.userPasswordRequirementsText;
                return false;
            }
            PasswordError = null;
            ValidateConfirmPassword();
            return true;
        }

        private bool ValidateConfirmPassword()
        {
            if (string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                ConfirmPasswordError = Properties.Langs.Lang.matchingPasswordErrorText;
                return false;
            }
            if (!ValidationUtils.PasswordsMatch(Password, ConfirmPassword))
            {
                ConfirmPasswordError = Properties.Langs.Lang.matchingPasswordErrorText;
                return false;
            }
            ConfirmPasswordError = null;
            return true;
        }
    }
}
