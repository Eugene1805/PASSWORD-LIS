using PASSWORD_LIS_Client.ViewModels;
using PASSWORD_LIS_Client.Views;
using PASSWORD_LIS_Client.WaitingRoomManagerServiceReference;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PASSWORD_LIS_Client.Utils
{
    public interface IWindowService
    {
        void Initialize(Frame frame);
        void GoBack();
        void NavigateTo(Page page);
        void ShowVerifyCodeWindow(string email, VerificationReason reason);
        void ShowChangePasswordWindow(string email, string verificationCode);
        void ShowLoginWindow();
        void CloseWindow(object viewModel);
        void ShowPopUp(string title, string message, PopUpIcon icon);
        bool ShowYesNoPopUp(string title, string message);
        void ShowReportWindow(PlayerDTO reportedPlayer);
        void ShowMainWindow();
        void CloseMainWindow();

    }

    public class WindowService : IWindowService
    {
        private Frame mainFrame;

        public void Initialize(Frame frame)
        {
            mainFrame = frame;
        }
        public void GoBack()
        {
            if (mainFrame?.NavigationService.CanGoBack == true)
            {
                mainFrame.NavigationService.GoBack();
            }
        }

        public void NavigateTo(Page page)
        {
            mainFrame?.NavigationService.Navigate(page);
        }
        public void ShowVerifyCodeWindow(string email, VerificationReason reason)
        {
            var viewModel = new VerifyCodeViewModel(email, reason, App.WindowService,
                App.VerificationCodeManagerService, App.PasswordResetManagerService);
            var window = new VerifyCodeWindow { DataContext = viewModel };
            window.Show();
        }
        public void ShowChangePasswordWindow(string email, string verificationCode)
        {
            var viewModel = new ChangePasswordViewModel(email, verificationCode, App.WindowService,
                App.PasswordResetManagerService);
            var window = new ChangePasswordWindow { DataContext = viewModel };
            window.Show();
        }

        public void ShowLoginWindow()
        {
            var loginViewModel = new LoginViewModel(App.LoginManagerService, App.WindowService);
            var loginWindow = new LoginWindow { DataContext = loginViewModel};
            loginWindow.Show();
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

        public bool ShowYesNoPopUp(string title, string message)
        {
            var viewModel = new YesNoPopUpViewModel(title, message);
            var popUpWindow = new YesNoPopUpWindow { DataContext = viewModel };

            bool? userResponse = popUpWindow.ShowDialog();

            return userResponse.HasValue && userResponse.Value;
        }

        public void ShowReportWindow(PlayerDTO reportedPlayer)
        {
            var reporter = SessionManager.CurrentUser;

            var reportViewModel = new ReportViewModel(reporter, reportedPlayer, App.WindowService, App.ReportManagerService);
            var reportWindow = new ReportWindow { DataContext = reportViewModel };
            reportWindow.ShowDialog();
        }

        public void ShowMainWindow()
        {
            var mainWindowViewModel = new MainWindowViewModel(this,App.BackgroundMusicService);
            var mainWindow = new MainWindow { DataContext = mainWindowViewModel};
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
        }
        public void CloseMainWindow()
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.Close();
            }
        }
    }
}
