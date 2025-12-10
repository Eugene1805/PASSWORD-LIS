using PASSWORD_LIS_Client.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace PASSWORD_LIS_Client.Views
{
    /// <summary>
    /// Lógica de interacción para LobbyPage.xaml
    /// </summary>
    public partial class LobbyPage : Page
    {
        public LobbyPage()
        {
            InitializeComponent();
            this.Loaded += LobbyPageLoaded;
        }

        private void LobbyPageLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is LobbyViewModel viewModel)
            {
                viewModel.LoadSessionData();
            }
        }
    }
}
