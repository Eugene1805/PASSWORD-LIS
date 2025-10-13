using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;

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
            if (SessionManager.IsUserLoggedIn() && SessionManager.CurrentUser.PlayerId < 0)
            {
                profileButton.IsEnabled = false;
                return; 
            }

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
            ConfigureGuestMode();
        }

        private void TopPlayersButonClick(object sender, RoutedEventArgs e)
        {
            var topPlayersWindow = new TopPlayersWindow();
            topPlayersWindow.ShowDialog();
        }

        private void ConfigureGuestMode()
        {
            if (SessionManager.IsUserLoggedIn() && SessionManager.CurrentUser.PlayerId < 0)
            {
               // profileButton.Visibility = Visibility.Visible;
                friendGrid.Visibility = Visibility.Collapsed;
                friendListBorder.Visibility = Visibility.Collapsed;   
            }
            
        }
    }
}
