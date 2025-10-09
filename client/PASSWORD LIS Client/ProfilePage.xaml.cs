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

namespace PASSWORD_LIS_Client
{
    /// <summary>
    /// Interaction logic for ProfilePage.xaml
    /// </summary>
    public partial class ProfilePage : Page
    {
        private readonly UserDTO currentUser;
        public ProfilePage(UserDTO user)
        {
            InitializeComponent();
            currentUser = user;
            LoadProfileData();
        }

        private void LoadProfileData()
        {
            if (currentUser == null)
            {
                return;
            }
            Nickname.Text = currentUser.Nickname;
            Name.Text = currentUser.FirstName;
            LastName.Text = currentUser.LastName;

            string avatarPath = GetAvatarPathById(currentUser.PhotoId);
            if (!string.IsNullOrEmpty(avatarPath))
            {
                Avatar.Fill = new ImageBrush { ImageSource = new BitmapImage(new Uri(avatarPath, UriKind.Relative)) };
            }
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
                default: return null;
            }
        }
        private void ButtonClickChooseAnAvatar(object sender, RoutedEventArgs e)
        {
            var chooseAvatarWindow = new ChooseAvatarWindow();
            if (chooseAvatarWindow.ShowDialog() == true)
            {
                int newAvatarId = chooseAvatarWindow.selectedAvatarId;

                currentUser.PhotoId = newAvatarId;
                string avatarPath = GetAvatarPathById(newAvatarId);
                if (!string.IsNullOrEmpty(avatarPath))
                {
                    Avatar.Fill = new ImageBrush { ImageSource = new BitmapImage(new Uri(avatarPath, UriKind.Relative)) };
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
            var client = new ProfileManagerClient();
            try
            {
                bool success = await client.UpdateAvatarAsync(currentUser.PlayerId, currentUser.PhotoId);

                if (success)
                {
                    MessageBox.Show("Avatar updated successfully!");
                    if (NavigationService.CanGoBack)
                    {
                        NavigationService.GoBack();
                    }
                } else
                {
                    MessageBox.Show("Failed to update avatar.");
                }
            } catch (Exception ex)
            {
                MessageBox.Show("Error de conexión: " + ex.Message);

            }
            finally
            {
                client.Close();
            }
            // Code to save changes goes here
            MessageBox.Show("Changes saved successfully!");
        }
    }
}
