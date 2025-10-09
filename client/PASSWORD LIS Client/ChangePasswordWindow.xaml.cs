using PASSWORD_LIS_Client.PasswordResetManagerServiceReference;
using System.Windows;
using System.Windows.Controls;

namespace PASSWORD_LIS_Client
{
    /// <summary>
    /// Interaction logic for ChangePasswordWindow.xaml
    /// </summary>
    public partial class ChangePasswordWindow : Window
    {
        private readonly string resetCode;
        private readonly string accountEmail;
        public ChangePasswordWindow()
        {
            InitializeComponent();
        }

        public ChangePasswordWindow(string code, string email) : this()
        {
            resetCode = code;
            accountEmail = email;
        }

        private void ButtonClickChangePassword(object sender, RoutedEventArgs e)
        {
            var client = new PasswordResetManagerClient();
            client.ResetPassword(new PasswordResetDTO
            {
                ResetCode = resetCode,
                NewPassword = newPasswordBox.Password,
                Email = accountEmail
            });
        }
    }
}
