using System.Windows;
using System.Windows.Controls;

namespace PASSWORD_LIS_Client.Views
{
    /// <summary>
    /// Interaction logic for WinnersPage.xaml
    /// </summary>
    public partial class WinnersPage : Page
    {
        public WinnersPage()
        {
            InitializeComponent();
        }

        private void ButtonClickBackToLobby(object sender, RoutedEventArgs e)
        {
            // Navigate back to the lobby page
            // NavigationService?.Navigate(new LobbyPage()); MODIFICAR EN UN FUTURO
        }

        private void ButtonClickPlayAgain(object sender, RoutedEventArgs e)
        {
            // TODO:
        }
    }
}
