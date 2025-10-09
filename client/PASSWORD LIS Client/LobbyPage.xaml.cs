using PASSWORD_LIS_Client.LoginManagerServiceReference;
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

namespace PASSWORD_LIS_Client
{
    /// <summary>
    /// Lógica de interacción para LobbyPage.xaml
    /// </summary>
    public partial class LobbyPage : Page
    {
        private readonly UserDTO currentUser;
        public LobbyPage(UserDTO user)
        {
            InitializeComponent();
            currentUser = user;

            LoadUserProfile();
        }

        private void LoadUserProfile()
        {
            if (currentUser == null)
            {
                return;
            }

            string avatarPath = GetAvatarPathById(currentUser.PhotoId);
            if (!string.IsNullOrEmpty(avatarPath)){
                var imageBrush = new System.Windows.Media.ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri(avatarPath, UriKind.Relative))
                };

                avatarEllipse.Fill = imageBrush;
            }
        }

        private void AvatarButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null)
            {
                NavigationService.Navigate(new ProfilePage(currentUser));
            }
        }

        private void AddFriendButtonClick(object sender, RoutedEventArgs e)
        {

        }

        private void ProfileButtonClick(object sender, RoutedEventArgs e)
        {

        }

        private void DeleteFriendButtonClick(object sender, RoutedEventArgs e)
        {

        }

        private string GetAvatarPathById(int photoId)
        {
            switch (photoId)
            {
                case 1: return "/Resources/Avatar1.png";
                case 2: return "/Resources/Avatar2.png";
                case 3: return "/Resources/Avatar3.png";
                case 4: return "/Resources/Avatar4.png";
                case 5: return "/Resources/Avatar5.png";
                case 6: return "/Resources/Avatar6.png";
                default: return null; // O una imagen por defecto
            }
        }
    }
}
