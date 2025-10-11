using PASSWORD_LIS_Client.PasswordResetManagerServiceReference;
using System;
using System.ServiceModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace PASSWORD_LIS_Client
{
    /// <summary>
    /// Interaction logic for RetrievePasswordWindow.xaml
    /// </summary>
    public partial class RetrievePasswordWindow : Window
    {
        public RetrievePasswordWindow()
        {
            InitializeComponent();
        }

        private async void ButtonClickSendCode(object sender, RoutedEventArgs e)
        {
            if (!IsInputValid())
            {
                return;
            }

            sendCodeButton.IsEnabled = false;
            try
            {
                bool success = await TryRequestResetCodeAsync(emailTextBox.Text);

                if (success)
                {
                    ProcessCodeRequestSuccess(emailTextBox.Text);
                }
                else
                {
                    MessageBox.Show("No se pudo enviar el código. Verifica que el correo sea correcto o espera un minuto antes de reintentar.", "Envío Fallido", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocurrió un error inesperado: {ex.Message}", "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                sendCodeButton.IsEnabled = true;
            }
        }

        private async Task<bool> TryRequestResetCodeAsync(string email)
        {
            var client = new PasswordResetManagerClient();
            try
            {
                var requestDto = new EmailVerificationDTO { Email = email, VerificationCode = "" };
                bool success = await client.RequestPasswordResetCodeAsync(requestDto);
                client.Close();
                return success;
            }
            catch (TimeoutException)
            {
                client.Abort();
                MessageBox.Show("El servidor no respondió a tiempo. Inténtalo de nuevo.", "Error de Red", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (EndpointNotFoundException)
            {
                client.Abort();
                MessageBox.Show("No se pudo conectar con el servidor. Verifica tu conexión a internet.", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (CommunicationException ex)
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

        private void ProcessCodeRequestSuccess(string email)
        {
            var verifyCodeWindow = new VerifyCodeWindow(email, VerificationReason.PasswordReset);
            bool? result = verifyCodeWindow.ShowDialog();

            if (result == true)
            {
                var changePasswordWindow = new ChangePasswordWindow(email, verifyCodeWindow.EnteredCode);
                changePasswordWindow.Show();
                this.Close();
            }
        }

        private bool IsInputValid()
        {
            string email = emailTextBox.Text;
            if (string.IsNullOrWhiteSpace(email))
            {
                MessageBox.Show(Properties.Langs.Lang.requiredFieldsText);
                return false;
            }
            string emailRegex = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";

            if(!Regex.IsMatch(email, emailRegex))
            {
                MessageBox.Show(Properties.Langs.Lang.invalidEmailErrorText);
                return false;
            }
            return true;
        }
    }
}
