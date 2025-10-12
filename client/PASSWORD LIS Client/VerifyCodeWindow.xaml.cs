using PASSWORD_LIS_Client.VerificationCodeManagerServiceReference;
using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;

namespace PASSWORD_LIS_Client
{
    /// <summary>
    /// Interaction logic for VerifyCodeWindow.xaml
    /// </summary>

    public enum VerificationReason
    {
        AccountActivation,
        PasswordReset
    }
    public partial class VerifyCodeWindow : Window
    {
        public string EnteredCode { get; private set; }
        private readonly string email;
        private readonly VerificationReason reason;
        public VerifyCodeWindow()
        {
            InitializeComponent();
        }

        public VerifyCodeWindow(string email, VerificationReason reason) : this()
        {
            emailTextblock.Text = email;
            this.reason = reason;
            this.email = email;
        }

        private async void ButtonClickVerifyCode(object sender, RoutedEventArgs e)
        {
            string code = verificationCodeTextBox.Text;
            if (string.IsNullOrWhiteSpace(code))
            {
                MessageBox.Show("Por favor, ingresa el código de verificación.", "Campo Requerido");
                return;
            }

            verifyCodeButton.IsEnabled = false;
            resendCodeHyperlink.IsEnabled = false; 

            bool isCodeValid = await TryVerifyCodeAsync(code);

            if (isCodeValid)
            {
                this.EnteredCode = code;
                this.DialogResult = true; 
            }
            else
            {
                MessageBox.Show("El código es incorrecto o ha expirado.", "Verificación Fallida");
                verifyCodeButton.IsEnabled = true;
                resendCodeHyperlink.IsEnabled = true;
            }
        }

        private async void HyperlinkClickResendCode(object sender, RoutedEventArgs e)
        {
            verifyCodeButton.IsEnabled = false;
            resendCodeHyperlink.IsEnabled = false;

            bool success = await TryResendCodeAsync();

            if (success)
            {
                MessageBox.Show("Se ha enviado un nuevo código a tu correo.", "Código Enviado");
            }
            else
            {
                MessageBox.Show("Por favor, espera al menos un minuto antes de solicitar otro código.", "Límite de Tiempo");
            }

            verifyCodeButton.IsEnabled = true;
            resendCodeHyperlink.IsEnabled = true;
        }
        
        private async Task<bool> TryVerifyCodeAsync(string code)
        {
            // Usamos esta interfaz común para poder abortar cualquier tipo de cliente en el catch
            System.ServiceModel.ICommunicationObject client = null;
            try
            {
                bool isValid = false;
                switch (reason)
                {
                    case VerificationReason.AccountActivation:
                        var activationClient = new AccountVerificationManagerClient();
                        client = activationClient;
                        var dto = new EmailVerificationDTO { Email = this.email, VerificationCode = code };
                        isValid = await activationClient.VerifyEmailAsync(dto);
                        break;

                    case VerificationReason.PasswordReset:
                        var resetClient = new PasswordResetManagerServiceReference.PasswordResetManagerClient();
                        client = resetClient; 
                        var resetDto = new PasswordResetManagerServiceReference.EmailVerificationDTO { Email = this.email, VerificationCode = code };
                        isValid = await resetClient.ValidatePasswordResetCodeAsync(resetDto);
                        break;
                }
                client.Close();
                return isValid;
            }
            catch (TimeoutException)
            {
                client?.Abort();
                MessageBox.Show("El servidor no respondió a tiempo. Inténtalo de nuevo.", "Error de Red");
                return false;
            }
            catch (EndpointNotFoundException)
            {
                client?.Abort();
                MessageBox.Show("No se pudo conectar con el servidor. Verifica tu conexión.", "Error de Conexión");
                return false;
            }
            catch (Exception ex)
            {
                client?.Abort();
                MessageBox.Show($"Ocurrió un error inesperado: {ex.Message}", "Error");
                return false;
            }
        }

        private async Task<bool> TryResendCodeAsync()
        {
            System.ServiceModel.ICommunicationObject client = null;
            try
            {
                bool success = false;
                switch (reason)
                {
                    case VerificationReason.AccountActivation:
                        var activationClient = new AccountVerificationManagerClient();
                        client = activationClient;
                        success = await activationClient.ResendVerificationCodeAsync(this.email);
                        break;
                    case VerificationReason.PasswordReset:
                        var resetClient = new PasswordResetManagerServiceReference.PasswordResetManagerClient();
                        client = resetClient;
                        var resetDto = new PasswordResetManagerServiceReference.EmailVerificationDTO { Email = this.email, VerificationCode = "" };
                        success = await resetClient.RequestPasswordResetCodeAsync(resetDto);
                        break;
                }
                client.Close();
                return success;
            }
            catch (TimeoutException)
            {
                client?.Abort();
                MessageBox.Show("El servidor no respondió a tiempo. Inténtalo de nuevo.", "Error de Red");
                return false;
            }
            catch (EndpointNotFoundException)
            {
                client?.Abort();
                MessageBox.Show("No se pudo conectar con el servidor. Verifica tu conexión.", "Error de Conexión");
                return false;
            }
            catch (Exception ex)
            {
                client?.Abort();
                MessageBox.Show($"Ocurrió un error inesperado: {ex.Message}", "Error");
                return false;
            }
        }
    }
}
