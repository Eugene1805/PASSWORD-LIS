using PASSWORD_LIS_Client.AccountManagerServiceReference;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PASSWORD_LIS_Client
{
    /// <summary>
    /// Interaction logic for SignUpWindow.xaml
    /// </summary>
    public partial class SignUpWindow : Window
    {
        private static readonly string tcUrl = "https://www.uv.mx/legislacion/files/2017/07/Codigo-de-etica-de-la-Universidad-Veracruzana.pdf";
        public SignUpWindow()
        {
                InitializeComponent();           
        }

        private async void ButtonClickSignUp(object sender, RoutedEventArgs e)
        {
            if (!CanExecuteSignUp())
            {
                MessageBox.Show(Properties.Langs.Lang.requiredFieldsText);
                return;
            }

            if (!IsValidEmail(emailTextBox.Text))
            {
                MessageBox.Show(Properties.Langs.Lang.invalidEmailErrorText);
                return;
            }

            if (!ArePasswordsMatching(passwordBox.Password, confirmPasswordBox.Password))
            {
                MessageBox.Show(Properties.Langs.Lang.matchingPasswordErrorText);
                return;
            }

            if (!ArePasswordRequirementsMet(passwordBox.Password))
            {
                MessageBox.Show(Properties.Langs.Lang.userPasswordRequirementsText);
                return;
            }

            AccountManagerClient client = null;
            bool accountCreated = false;
            signUpButton.IsEnabled = false;
            try
            {
                client = new AccountManagerClient();

                var userAccount = new NewAccountDTO
                {
                    Email = emailTextBox.Text,
                    Password = passwordBox.Password, 
                    FirstName = firstNameTextBox.Text,
                    LastName = lastNameTextBox.Text,
                    Nickname = nicknameTextBox.Text
                };

                accountCreated = await client.CreateAccountAsync(userAccount);

                client.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error de conexión con el servidor: " + ex.Message);
                client.Abort(); 
            }
            finally
            {
                signUpButton.IsEnabled = true;
            }

            if (accountCreated)
            {
                var codeVerificationWindow = new VerifyCodeWindow(emailTextBox.Text, VerificationReason.AccountActivation);
                bool? result = codeVerificationWindow.ShowDialog();

                if (result == true)
                {
                    var loginWindow = new LoginWindow();
                    loginWindow.Show();
                    this.Close();
                }
            }
            else
            {
                MessageBox.Show("No se pudo crear la cuenta. El correo o nickname ya podría estar en uso.");
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
            try
            {
                Process.Start(new ProcessStartInfo(tcUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo abrir el enlace: " + ex.Message);
            }
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
        private static bool ArePasswordsMatching(string password, string confirmPassword)
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
