using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.ViewModels;
using PASSWORD_LIS_Client.Views;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PASSWORD_LIS_Client
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Creamos las dependencias
            var loginService = new WcfLoginManagerService();
            var windowService = new WindowService();

            // 2. Creamos el ViewModel, inyectando las dependencias
            var loginViewModel = new LoginViewModel(loginService, windowService);

            // 3. Creamos la vista y le asignamos el ViewModel
            var loginWindow = new LoginWindow {DataContext = loginViewModel};
            this.MainWindow = loginWindow;  
            // 4. Mostramos la ventana
            loginWindow.Show();

        }
    }
}
