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
using PASSWORD_LIS_Client.LoginManagerServiceReference;
using System.Windows.Shapes;
using PASSWORD_LIS_Client.ProfileManagerServiceReference;
using PASSWORD_LIS_Client.Utils;

namespace PASSWORD_LIS_Client
{
    /// <summary>
    /// Interaction logic for ProfilePage.xaml
    /// </summary>
    public partial class ProfilePage : Page
    {
        public ProfilePage()
        {
            InitializeComponent();
            LoadProfileData();
        }

        private void LoadProfileData()
        {
            if (!SessionManager.IsUserLoggedIn())
            {
                return;
            }

            var currentUser = SessionManager.CurrentUser;
            Nickname.Text = currentUser.Nickname;
            Name.Text = currentUser.FirstName;
            LastName.Text = currentUser.LastName;

            Uri avatarUri = GetAvatarUriById(currentUser.PhotoId);
            if (avatarUri != null)
            {
                var imageBrush = new ImageBrush
                {
                    ImageSource = new BitmapImage(avatarUri)
                };

                AvatarEllipse.Fill = imageBrush;
            }
        }


        private void ButtonClickChooseAnAvatar(object sender, RoutedEventArgs e)
        {
            var chooseAvatarWindow = new ChooseAvatarWindow();
            if (chooseAvatarWindow.ShowDialog() == true)
            {
                int newAvatarId = chooseAvatarWindow.selectedAvatarId;
                SessionManager.CurrentUser.PhotoId = newAvatarId;

                Uri avatarUri = GetAvatarUriById(newAvatarId);
                if (avatarUri != null)
                {
                    AvatarEllipse.Fill = new ImageBrush { ImageSource = new BitmapImage(avatarUri) };
                }
            }
        }
        private void ButtonClickEditProfile(object sender, RoutedEventArgs e)
        {
            // Code to edit profile goes here
            MessageBox.Show("Edit Profile clicked!");
        }
        private void ButtonClickChangePassword(object sender, RoutedEventArgs e)
        {
            // Code to change password goes here
            MessageBox.Show("Change Password clicked!");
        }
        private async void ButtonClickSaveChanges(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.IsUserLoggedIn())
            {
                MessageBox.Show("No user is logged in."); //Checar
                return;
            }

            var client = new ProfileManagerClient();
            try
            {
                var currentUser = SessionManager.CurrentUser;
                bool success = await client.UpdateAvatarAsync(currentUser.PlayerId, currentUser.PhotoId);

                if (success)
                {
                    MessageBox.Show("Avatar updated successfully!");
                    if (NavigationService.CanGoBack)
                    {
                        NavigationService.GoBack();
                    }
                }
                else
                {
                    MessageBox.Show("Failed to update avatar.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error de conexión: " + ex.Message);

            }
            finally
            {
                client.Close();
            }
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
            string packUri = $"pack://application:,,,{resourcePath}";
            return new Uri(packUri, UriKind.Absolute);
        }
    } 
    }
