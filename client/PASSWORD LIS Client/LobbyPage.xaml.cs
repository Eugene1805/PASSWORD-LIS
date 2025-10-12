using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PASSWORD_LIS_Client.Utils;

namespace PASSWORD_LIS_Client
{
    /// <summary>
    /// Lógica de interacción para LobbyPage.xaml
    /// </summary>
    public partial class LobbyPage : Page
    {
        public LobbyPage()
        {
            InitializeComponent();
        }

        private void LoadUserProfile()
        {
            if (!SessionManager.IsUserLoggedIn ())
            {
                return;
            }

            Uri avatarUri = AvatarHelper.GetAvatarUriById(SessionManager.CurrentUser.PhotoId);
            if (avatarUri != null)
            {
                avatarEllipse.Fill = new ImageBrush { ImageSource = new BitmapImage(avatarUri) };
            }
        }

        private void AvatarButton_Click(object sender, RoutedEventArgs e)
        {
           
        }

        private void AddFriendButtonClick(object sender, RoutedEventArgs e)
        {

        }

        private void ProfileButtonClick(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null)
            {
                NavigationService.Navigate(new ProfilePage());
            }
        }

        private void DeleteFriendButtonClick(object sender, RoutedEventArgs e)
        {

        }
        private void LobbyPageLoaded(object sender, RoutedEventArgs e)
        {
            LoadUserProfile();
        }

        private void TopPlayersButonClick(object sender, RoutedEventArgs e)
        {
            var topPlayersWindow = new TopPlayersWindow();
            topPlayersWindow.ShowDialog();
        }
    }
}
