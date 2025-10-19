using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.ViewModels;
using PASSWORD_LIS_Client.Views;
using System.Windows;

namespace PASSWORD_LIS_Client
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IWindowService WindowService { get; private set; }
        public static ILoginManagerService LoginManagerService { get; private set; }
        public static IWaitingRoomManagerService WaitRoomManagerService { get; private set; }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Creamos las dependencias
            LoginManagerService = new WcfLoginManagerService();
            WindowService = new WindowService();
            WaitRoomManagerService = new WcfWaitingRoomManagerService();
            // 2. Creamos el ViewModel, inyectando las dependencias
            var loginViewModel = new LoginViewModel(LoginManagerService, WindowService);

            // 3. Creamos la vista y le asignamos el ViewModel
            var loginWindow = new LoginWindow {DataContext = loginViewModel};
            //this.MainWindow = loginWindow;  
            // 4. Mostramos la ventana
            loginWindow.Show();

        }
    }
}
