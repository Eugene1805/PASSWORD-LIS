using PASSWORD_LIS_Client.VerificationCodeManagerServiceReference;
using System;
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
            bool isCodeValid = false;
            verifyCodeButton.IsEnabled = false;
            EnteredCode = verificationCodeTextBox.Text;
            try
            {
                switch (reason)
                {
                    case VerificationReason.AccountActivation:
                        var verificationClient = new AccountVerificationManagerClient();
                        var dto = new EmailVerificationDTO
                        {
                            Email = email,
                            VerificationCode = verificationCodeTextBox.Text
                        };
                        isCodeValid = await verificationClient.VerifyEmailAsync(dto);
                        verificationClient.Close();
                        break;

                    case VerificationReason.PasswordReset:
                      
                        var resetClient = new PasswordResetManagerServiceReference.PasswordResetManagerClient();
                        isCodeValid = await resetClient.ValidatePasswordResetCodeAsync(new PasswordResetManagerServiceReference.EmailVerificationDTO
                        {
                            Email = email,
                            VerificationCode = verificationCodeTextBox.Text
                        });
                        MessageBox.Show(isCodeValid.ToString());
                        resetClient.Close();
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error de conexión: " + ex.Message);
                isCodeValid = false;
            }
            finally
            {
                verifyCodeButton.IsEnabled = true;
            }

            if (isCodeValid)
            {
                this.DialogResult = true;
            }
            else
            {
                MessageBox.Show("El código es incorrecto o ha expirado.");
            }
        }

        private async void HyperlinkClickResendCode(object sender, RoutedEventArgs e)
        {
            bool success = false;
            try
            {
                switch (reason)
                {
                    case VerificationReason.AccountActivation:
                        var activationClient = new AccountVerificationManagerClient();
                        success = await activationClient.ResendVerificationCodeAsync(email);
                        activationClient.Close();
                        break;
                    case VerificationReason.PasswordReset:
                        var resetClient = new PasswordResetManagerServiceReference.PasswordResetManagerClient();
                        success = await resetClient.RequestPasswordResetCodeAsync(new PasswordResetManagerServiceReference.EmailVerificationDTO
                        {
                            Email = email,
                            VerificationCode = verificationCodeTextBox.Text
                        });
                        resetClient.Close();
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error de conexión al reenviar el código: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            }
            

            if (success)
            {
                MessageBox.Show("Se ha enviado un nuevo código a tu correo.");
            }
            else
            {
                MessageBox.Show("Por favor, espera al menos un minuto antes de solicitar otro código.");
            }

        }
    }
}
