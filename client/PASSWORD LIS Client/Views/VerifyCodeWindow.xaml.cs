using System.Windows;

namespace PASSWORD_LIS_Client.Views
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
        public VerifyCodeWindow()
        {
            InitializeComponent();
        }
    }
}
