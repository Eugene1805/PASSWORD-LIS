using log4net;
using PASSWORD_LIS_Client.GameManagerServiceReference;
using PASSWORD_LIS_Client.ViewModels;
using PASSWORD_LIS_Client.Views;
using PASSWORD_LIS_Client.WaitingRoomManagerServiceReference;
using System;
using System.Collections.Generic;
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
        void ShowReportWindow(WaitingRoomManagerServiceReference.PlayerDTO reportedPlayer);
        void ShowMainWindow();
        void CloseMainWindow();

    }

    public class WindowService : IWindowService
    {
        private Frame mainFrame;
        private static readonly ILog log = LogManager.GetLogger(typeof(WindowService));

        public void Initialize(Frame frame)
        {
            mainFrame = frame;
        }
        public void GoBack()
        {
            try
            {
                log.Info("WindowService.GoBack called");

                if (Application.Current.MainWindow != null)
                {
                    var mainWindow = Application.Current.MainWindow;
                    if (mainWindow.Content is Frame frame && frame.CanGoBack)
                    {
                        log.Info("Navigating frame back");
                        frame.GoBack();
                        log.Info("Frame navigation completed");
                    }
                    else
                    {
                        log.Warn("Cannot navigate back - no frame or CanGoBack is false");
                    }
                }
                else
                {
                    log.Warn("Application.Current.MainWindow is null");
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error in WindowService.GoBack: {ex.Message}", ex);
                throw;
            }
        }

        public void NavigateTo(Page page)
        {
            try
            {
                log.Info($"NavigateTo called - Page: {page.GetType().Name}, mainFrame null: {mainFrame == null}");

                if (mainFrame != null)
                {
                    mainFrame.NavigationService.Navigate(page);
                    log.Info($"Navigation completed - CanGoBack: {mainFrame.CanGoBack}");
                }
                else
                {
                    log.Error("mainFrame is null in NavigateTo!");

                    // Fallback: usar el Frame de MainWindow directamente
                    if (Application.Current.MainWindow is MainWindow mainWindow && mainWindow.mainFrame != null)
                    {
                        log.Info("Using MainWindow's mainFrame as fallback");
                        mainWindow.mainFrame.Navigate(page);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error in NavigateTo: {ex.Message}", ex);
            }
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

        public void ShowReportWindow(WaitingRoomManagerServiceReference.PlayerDTO reportedPlayer)
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
