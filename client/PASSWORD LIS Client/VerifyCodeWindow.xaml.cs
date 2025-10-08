using PASSWORD_LIS_Client.VerificationCodeManagerServiceReference;
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
                MessageBox.Show("Codigo verificado con exito");
            }
        }
    }
}
