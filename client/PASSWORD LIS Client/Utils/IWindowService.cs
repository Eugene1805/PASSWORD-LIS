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
        void GoToLobby();

        void ReturnToLogin();

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

                if (mainFrame != null)
                {
                    if (mainFrame.CanGoBack)
                    {
                        log.Info("Navigating mainFrame back");
                        mainFrame.GoBack();
                    }
                    else
                    {
                        log.Warn("mainFrame exists but Cannot GoBack (History is empty)");
                    }
                    return;
                }

                if (Application.Current.MainWindow != null)
                {
                    var window = Application.Current.MainWindow;

                    if (window is MainWindow myWindow && myWindow.mainFrame != null && myWindow.mainFrame.CanGoBack)
                    {
                        log.Info("Navigating MainWindow.mainFrame back via Fallback");
                        myWindow.mainFrame.GoBack();
                    }
                    else if (window.Content is Frame frame && frame.CanGoBack)
                    {
                        log.Info("Navigating window content frame back");
                        frame.GoBack();
                    }
                    else
                    {
                        log.Warn("Cannot navigate back - Frame not found or CanGoBack is false");
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error in WindowService.GoBack: {ex.Message}", ex);
            }
        }

        public void NavigateTo(Page page)
        {
            try
            {
                log.InfoFormat("NavigateTo called - Page: {0}, mainFrame null: {1}", page.GetType().Name, mainFrame == null);

                if (mainFrame != null)
                {
                    mainFrame.NavigationService.Navigate(page);
                    log.InfoFormat("Navigation completed - CanGoBack: {0}", mainFrame.CanGoBack);
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
            var loginWindow = new LoginWindow { DataContext = loginViewModel };
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
            var mainWindowViewModel = new MainWindowViewModel(this, App.BackgroundMusicService);
            var mainWindow = new MainWindow { DataContext = mainWindowViewModel };
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

        public void GoToLobby()
        {
            var lobbyViewModel = new LobbyViewModel(this, App.FriendsManagerService, App.WaitRoomManagerService, App.ReportManagerService);
            var lobbyPage = new LobbyPage { DataContext = lobbyViewModel };
            NavigateTo(lobbyPage);
        }

        public void ReturnToLogin()
        {
            ShowLoginWindow();

            CloseMainWindow();
        }
    }
}
