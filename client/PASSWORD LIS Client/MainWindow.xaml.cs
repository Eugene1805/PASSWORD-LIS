using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.ViewModels;
using PASSWORD_LIS_Client.Views;
using System;
using System.Windows;

namespace PASSWORD_LIS_Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            (App.WindowService as WindowService)?.Initialize(mainFrame);
            SetInitialPage();
        }

        public void SetInitialPage()
        {
            if (SessionManager.IsUserLoggedIn())
            {
                var lobbyViewModel = new LobbyViewModel(App.WindowService, App.FriendsManagerService, App.WaitRoomManagerService);

                var lobbyPage = new LobbyPage { DataContext = lobbyViewModel };

                App.WindowService.NavigateTo(lobbyPage);
            }
            else
            {
                var loginViewModel = new LoginViewModel(App.LoginManagerService, App.WindowService);
                var loginWindow = new LoginWindow { DataContext = loginViewModel };
                loginWindow.Show();
                this.Close();
            }

        }

    }
}
