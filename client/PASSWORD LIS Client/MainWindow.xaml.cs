using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.ViewModels;
using PASSWORD_LIS_Client.Views;
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
                // --- LÓGICA CORREGIDA ---
                // 1. Creamos las dependencias para el LobbyViewModel
                //var lobbywindowService = new WindowService();
                var friendsService = new WcfFriendsManagerService();
                // 2. Creamos el ViewModel, inyectando las dependencias
                var lobbyViewModel = new LobbyViewModel(App.WindowService, friendsService);

                // 3. Creamos la vista (la página) y le ASIGNAMOS el ViewModel
                var lobbyPage = new LobbyPage { DataContext = lobbyViewModel };

                // 4. Navegamos a la página ya configurada
                //mainFrame.NavigationService.Navigate(lobbyPage);
                App.WindowService.NavigateTo(lobbyPage);
            }
            else
            {
                // La lógica defensiva para volver al login
                // var loginWindow = new LoginWindow(); // Asumiendo que LoginWindow está en Views
                //loginWindow.Show();
                //this.Close();
                var loginViewModel = new LoginViewModel(App.LoginManagerService, App.WindowService);
                var loginWindow = new LoginWindow { DataContext = loginViewModel };
                loginWindow.Show();
                this.Close();
            }

        }
    }
}
