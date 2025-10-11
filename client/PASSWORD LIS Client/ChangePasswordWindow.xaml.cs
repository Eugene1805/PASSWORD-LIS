using PASSWORD_LIS_Client.PasswordResetManagerServiceReference;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PASSWORD_LIS_Client
{
    /// <summary>
    /// Interaction logic for ChangePasswordWindow.xaml
    /// </summary>
    public partial class ChangePasswordWindow : Window
    {
        private readonly string accountEmail;
        private readonly string verificationCode;
        public ChangePasswordWindow()
        {
            InitializeComponent();
        }

        public ChangePasswordWindow(string email, string code) : this()
        {
            accountEmail = email;
            verificationCode = code;
        }

        private async void ButtonClickChangePassword(object sender, RoutedEventArgs e)
        {
            if (!IsInputValid())
            {
                return;
            }

            changePasswordButton.IsEnabled = false;
            try
            {
                bool success = await TryResetPasswordOnServerAsync();

                if (success)
                {
                    ProcessSuccessfulPasswordChange();
                }
                else
                {
                    MessageBox.Show("No se pudo cambiar la contraseña. Revisa los mensajes de error anteriores.", "Operación Fallida", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocurrió un error inesperado: {ex.Message}", "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                changePasswordButton.IsEnabled = true;
            }
        }
        private async Task<bool> TryResetPasswordOnServerAsync()
        {
            var client = new PasswordResetManagerClient();
            try
            {
                var passwordResetInfo = new PasswordResetDTO
                {
                    NewPassword = newPasswordBox.Password,
                    Email = this.accountEmail,
                    ResetCode = this.verificationCode
                };

                bool success = await client.ResetPasswordAsync(passwordResetInfo);

                client.Close();
                return success;
            }
            catch (TimeoutException)
            {
                client.Abort();
                MessageBox.Show("El servidor no respondió a tiempo. Por favor, inténtalo más tarde.", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (System.ServiceModel.EndpointNotFoundException)
            {
                client.Abort();
                MessageBox.Show("No se pudo conectar con el servidor. Verifica tu conexión a internet.", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (System.ServiceModel.CommunicationException ex)
            {
                client.Abort();
                MessageBox.Show($"Ocurrió un error de comunicación: {ex.Message}", "Error de Red", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                client.Abort();
                MessageBox.Show($"Ocurrió un error inesperado: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        private void ProcessSuccessfulPasswordChange()
        {
            MessageBox.Show("Contraseña cambiada exitosamente. Ahora serás redirigido al inicio de sesión.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
        private bool IsInputValid()
        {
            string newPassword = newPasswordBox.Password;
            string confirmPassword = confirmNewPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                MessageBox.Show(Properties.Langs.Lang.requiredFieldsText);
                return false;
            }

            if (newPassword != confirmPassword)
            {
                MessageBox.Show(Properties.Langs.Lang.matchingPasswordErrorText);
                return false;
            }

            return true;
        }
    }
}
