using System.Windows;

namespace PASSWORD_LIS_Client.Views
{

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
