using PASSWORD_LIS_Client.ViewModels;
using PASSWORD_LIS_Client.Views;
using System.Linq;
using System.Windows;

namespace PASSWORD_LIS_Client.Utils
{
    public interface IWindowService
    {
        string ShowVerifyCodeDialog(string email, VerificationReason reason);
        void ShowChangePasswordWindow(string email, string verificationCode);
        void ShowLoginWindow();
        void CloseWindow(object viewModel);
        void ShowPopUp(string title, string message, PopUpIcon icon);

    }

    public class WindowService : IWindowService
    {
        public string ShowVerifyCodeDialog(string email, VerificationReason reason)
        {
            var viewModel = new VerifyCodeViewModel(email, reason, this);
            var window = new VerifyCodeWindow { DataContext = viewModel };

            bool? result = window.ShowDialog();

            if (result == true)
            {
                return viewModel.EnteredCode;
            }
            return null;
        }

        public void ShowChangePasswordWindow(string email, string verificationCode)
        {
            var viewModel = new ChangePasswordViewModel(email, verificationCode, this);
            var window = new ChangePasswordWindow { DataContext = viewModel };
            window.Show();
        }
        
        public void ShowLoginWindow()
        {
            var window = new LoginWindow();
            window.Show();
        }

        public void CloseWindow(object viewModel)
        {
            Window windowToClose = Application.Current.Windows
                .OfType<Window>()
                .FirstOrDefault(w => w.DataContext == viewModel);

            windowToClose?.Close();
        }

        public void ShowPopUp(string title, string message, PopUpIcon icon)
        {
            var popUp = new PopUpWindow(title, message, icon);
            popUp.ShowDialog();
        }
    }
}
