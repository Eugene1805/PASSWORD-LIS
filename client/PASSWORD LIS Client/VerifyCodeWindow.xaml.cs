using PASSWORD_LIS_Client.VerificationCodeManagerServiceReference;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace PASSWORD_LIS_Client
{
    /// <summary>
    /// Interaction logic for VerifyCodeWindow.xaml
    /// </summary>
    public partial class VerifyCodeWindow : Window
    {
        public VerifyCodeWindow()
        {
            InitializeComponent();
        }

        public VerifyCodeWindow(string email) : this()
        {
            emailLabel.Content = email;
        }

        private void ButtonClickVerifyCode(object sender, RoutedEventArgs e)
        {
            var emailVerificationDTO = new EmailVerificationDTO { 
                Email = emailLabel.Content.ToString(),
                VerificationCode = verificationCodeTextBox.Text
            };

            var verificationCodeManager = new AccountVerificationManagerClient();
            
            if (verificationCodeManager.VerifyEmail(emailVerificationDTO))
            {
                MessageBox.Show(Properties.Langs.Lang.codeActivationMessageText);
                var loginWindow = new LoginWindow();
                loginWindow.Show();
                this.Close();
            }
        }

        private async void HyperlinkClickResendCode(object sender, RoutedEventArgs e)
        {
            
            var client = new AccountVerificationManagerClient();
            bool success = false;
            try
            {
                success = await client.ResendVerificationCodeAsync(emailLabel.Content.ToString());
                client.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error de conexión al intentar reenviar el código: " + ex.Message);
                client?.Abort();
            }

            if (success)
            {
                MessageBox.Show("Se ha enviado un nuevo código a tu correo.");
                var changePasswordWindow = new ChangePasswordWindow();
                changePasswordWindow.Show();
                this.Close();
            }
            else
            {
                MessageBox.Show("Por favor, espera al menos un minuto antes de solicitar otro código.");
            }

        }
    }
}
