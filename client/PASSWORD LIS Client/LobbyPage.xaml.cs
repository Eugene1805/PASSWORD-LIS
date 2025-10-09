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

            Uri avatarUri = GetAvatarUriById(currentUser.PhotoId);
            if (avatarUri != null){
                var imageBrush = new System.Windows.Media.ImageBrush
                {
                    ImageSource = new BitmapImage(avatarUri)
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

        private Uri GetAvatarUriById(int photoId)
        {
            string resourcePath;
            switch (photoId)
            {
                case 1:
                    resourcePath = "/Resources/Avatar1.png";
                    break;
                case 2:
                    resourcePath = "/Resources/Avatar2.png";
                    break;
                case 3:
                    resourcePath = "/Resources/Avatar3.png";
                    break;
                case 4:
                    resourcePath = "/Resources/Avatar4.png";
                    break;
                case 5:
                    resourcePath = "/Resources/Avatar5.png";
                    break;
                case 6:
                    resourcePath = "/Resources/Avatar6.png";
                    break;
                default:
                    return null; // O una imagen por defecto
            }

            // Construimos la URI de tipo "Pack"
            // Esto le dice a WPF que busque el recurso DENTRO del ensamblado.
            string packUri = $"pack://application:,,,{resourcePath}";
            return new Uri(packUri, UriKind.Absolute);
        }
    }
}
