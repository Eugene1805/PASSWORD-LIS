using PASSWORD_LIS_Client.AccountManagerServiceReference;
using PASSWORD_LIS_Client.Commands;
using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.ServiceModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class SignUpViewModel : BaseViewModel
    {
        #region Properties
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
        #endregion

        #region Commands
        public ICommand SignUpCommand { get; }
        public ICommand NavigateToLoginCommand { get; }
        public ICommand OpenTermsAndConditionsCommand { get; }
        #endregion

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
                MessageBox.Show(Properties.Langs.Lang.userAlreadyExistText, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (TimeoutException)
            {
                MessageBox.Show("El servidor tardó demasiado en responder. Por favor, inténtalo de nuevo.", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show("No se pudo conectar con el servidor. Verifica tu conexión a internet o inténtalo más tarde.", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show($"Ocurrió un error de comunicación con el servidor: {ex.Message}", "Error de Comunicación", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocurrió un error inesperado: {ex.Message}", "Error Inesperado", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("El enlace no está disponible actualmente", "Enlace Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(TCLink) { UseShellExecute = true });
            }
            catch (Win32Exception ex)
            {
                MessageBox.Show(
                    "No se pudo abrir el enlace. Asegúrate de tener un navegador web instalado y configurado como predeterminado.\n\nError: " + ex.Message,
                    "Error del Sistema"
                );
            }
            catch (ArgumentNullException)
            {
                MessageBox.Show(
                    "El enlace de los términos y condiciones no está disponible.",
                    "Error Interno"
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Ocurrió un error inesperado al intentar abrir el enlace.\n\nError: " + ex.Message,
                    "Error Desconocido"
                );
            }
        }

        #region Private Helper Methods
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
            if (!IsValidEmail(Email))
            {
                MessageBox.Show(Properties.Langs.Lang.invalidEmailErrorText);
                return false;
            }
            if (!PasswordsMatch(Password, ConfirmPassword))
            {
                MessageBox.Show(Properties.Langs.Lang.matchingPasswordErrorText);
                return false;
            }
            if (!ArePasswordRequirementsMet(Password))
            {
                MessageBox.Show(Properties.Langs.Lang.userPasswordRequirementsText);
                return false;
            }
            return true;
        }

        // Métodos de validación estáticos (pueden ir en una clase de utilidad si se prefieren)
        private static bool ArePasswordRequirementsMet(string password)
        {
            string passwordRegex = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^a-zA-Z0-9]).{8,15}$";
            return Regex.IsMatch(password, passwordRegex);
        }

        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            string emailRegex = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
            return Regex.IsMatch(email, emailRegex);
        }

        private static bool PasswordsMatch(string password, string confirmPassword)
        {
            return password == confirmPassword;
        }
        #endregion
    }
}
