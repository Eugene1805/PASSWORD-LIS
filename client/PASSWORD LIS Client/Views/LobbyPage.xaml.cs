using PASSWORD_LIS_Client.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace PASSWORD_LIS_Client.Views
{
    public partial class LobbyPage : Page
    {
        public LobbyPage()
        {
            InitializeComponent();
            this.Loaded += LobbyPageLoaded;
        }

        private void LobbyPageLoaded(object sender, RoutedEventArgs events)
        {
            if (DataContext is LobbyViewModel viewModel)
            {
                viewModel.LoadSessionData();
            }
        }
    }
}
