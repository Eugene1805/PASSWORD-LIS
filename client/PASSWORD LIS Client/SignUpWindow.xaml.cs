using PASSWORD_LIS_Client.AccountManagerServiceReference;
using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.ServiceModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace PASSWORD_LIS_Client
{
    /// <summary>
    /// Interaction logic for SignUpWindow.xaml
    /// </summary>
    public partial class SignUpWindow : Window
    {
        public string TCLink { get; }
        public SignUpWindow()
        {
            InitializeComponent();
            TCLink = ConfigurationManager.AppSettings["TCPageURL"];
        }

        private async void ButtonClickSignUp(object sender, RoutedEventArgs e)
        {
            if (!IsInputValid())
            {
                return;
            }

            signUpButton.IsEnabled = false;
            try
            {
                await TryCreateAccountOnServerAsync();

                ProcessSuccessfulSignUp(emailTextBox.Text);
                
            }
            catch (FaultException<ServiceErrorDetailDTO>)
            {
                MessageBox.Show(Properties.Langs.Lang.userAlreadyExistText);
            }
            catch (TimeoutException)
            {
                MessageBox.Show("El servidor tardó demasiado en responder. Por favor, inténtalo de nuevo.");

            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show("No se pudo conectar con el servidor. Verifica tu conexión a internet o inténtalo más tarde.");

            }
            catch (CommunicationException ex)
            {
                MessageBox.Show($"Ocurrió un error de comunicación con el servidor: {ex.Message}");

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocurrió un error inesperado: {ex.Message}");
            }
            finally
            {
                signUpButton.IsEnabled = true;
            }
        }

        private void HyperlinkClickLogIn(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        private void HyperlinkClickTC(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TCLink) || !Uri.IsWellFormedUriString(TCLink, UriKind.Absolute))
            {
                MessageBox.Show("El enlace no esta disponible actualmente", "Enlace Inválido");
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
        private async Task TryCreateAccountOnServerAsync()
        {
            var userAccount = new NewAccountDTO
            {
                Email = emailTextBox.Text,
                Password = passwordBox.Password,
                FirstName = firstNameTextBox.Text,
                LastName = lastNameTextBox.Text,
                Nickname = nicknameTextBox.Text
            };

            AccountManagerClient client = new AccountManagerClient();
            try
            {
                await client.CreateAccountAsync(userAccount);
                client.Close();
            }
            
            catch (Exception)
            {
                // Un error inesperado que no es de comunicación
                client.Abort();
                throw;

            }
        }
        private void ProcessSuccessfulSignUp(string userEmail)
        {
            var codeVerificationWindow = new VerifyCodeWindow(userEmail, VerificationReason.AccountActivation);
            bool? result = codeVerificationWindow.ShowDialog();

            if (result == true)
            {
                var loginWindow = new LoginWindow();
                loginWindow.Show();
                this.Close();
            }
        }
        private bool IsInputValid()
        {
            if (!CanExecuteSignUp())
            {
                MessageBox.Show(Properties.Langs.Lang.requiredFieldsText);
                return false;
            }
            if (!IsValidEmail(emailTextBox.Text))
            {
                MessageBox.Show(Properties.Langs.Lang.invalidEmailErrorText);
                return false;
            }
            if (!PasswordsMatch(passwordBox.Password, confirmPasswordBox.Password))
            {
                MessageBox.Show(Properties.Langs.Lang.matchingPasswordErrorText);
                return false;
            }
            if (!ArePasswordRequirementsMet(passwordBox.Password))
            {
                MessageBox.Show(Properties.Langs.Lang.userPasswordRequirementsText);
                return false;
            }
            return true;
        }

        private static bool ArePasswordRequirementsMet(string password)
        {
            // Use 8-15 characters including at least one uppercase, one lowercase, one numeric character and one symbol.

            string passwordRegex = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^a-zA-Z0-9]).{8,15}$";

            return Regex.IsMatch(password, passwordRegex);
        }
        public static bool IsValidEmail(string email)
        {
            string emailRegex = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";

            return Regex.IsMatch(email, emailRegex);
        }
        private static bool PasswordsMatch(string password, string confirmPassword)
        {
            return password == confirmPassword;
        }
        private bool CanExecuteSignUp()
        {
            return !string.IsNullOrEmpty(emailTextBox.Text) && !string.IsNullOrEmpty(firstNameTextBox.Text)
                && !string.IsNullOrEmpty(lastNameTextBox.Text) && !string.IsNullOrEmpty(nicknameTextBox.Text)
                && !string.IsNullOrEmpty(passwordBox.Password) && !string.IsNullOrEmpty(confirmPasswordBox.Password);
        }
    }
}
