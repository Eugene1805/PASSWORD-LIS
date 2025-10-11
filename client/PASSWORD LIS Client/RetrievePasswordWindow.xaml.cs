using PASSWORD_LIS_Client.PasswordResetManagerServiceReference;
using PASSWORD_LIS_Client.VerificationCodeManagerServiceReference;
using System;
using System.ComponentModel;
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
            var client = new PasswordResetManagerClient();
            bool success = false;
            try
            {
                success = await client.RequestPasswordResetCodeAsync(new PasswordResetManagerServiceReference.EmailVerificationDTO { Email = emailTextBox.Text, 
                VerificationCode = ""});
                client.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                client.Abort();
            }

            if (success)
            {
                var verifyCodeWindow = new VerifyCodeWindow(emailTextBox.Text, VerificationReason.PasswordReset);

                bool? result = verifyCodeWindow.ShowDialog();
                this.Close();
                if (result == true)
                {
                    var changePasswordWindow = new ChangePasswordWindow(emailTextBox.Text, verifyCodeWindow.EnteredCode); // Pasa los datos necesarios
                    changePasswordWindow.Show();
                    this.Close();
                }
            }
            else
            {
                MessageBox.Show("No se pudo enviar el código. Verifica el correo o espera un minuto.");
            }
        }
    }
}
