using System.Windows;
using System.Windows.Controls;

namespace PASSWORD_LIS_Client.Views
{
    /// <summary>
    /// Interaction logic for LosersPage.xaml
    /// </summary>
    public partial class LosersPage : Page
    {
        public LosersPage()
        {
            InitializeComponent();
        }

        private void ButtonClickPlayAgain(object sender, RoutedEventArgs e)
        {
            // TODO: Implement play again logic
        }

        private void ButtonClickBackToLobby(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
        }
    }
}
