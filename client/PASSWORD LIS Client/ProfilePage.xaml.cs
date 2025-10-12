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
using PASSWORD_LIS_Client.ProfileManagerServiceReference;
using SessionUserDTO = PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO;

namespace PASSWORD_LIS_Client
{
    /// <summary>
    /// Interaction logic for ProfilePage.xaml
    /// </summary>
    public partial class ProfilePage : Page
    {
        private bool isEditMode = false;
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
            nicknameTextBox.Text = currentUser.Nickname;
            nameTextBox.Text = currentUser.FirstName;
            lastNameTextBox.Text = currentUser.LastName;
            
            if (currentUser.SocialAccounts != null)
            {
                facebookTextBox.Text = currentUser.SocialAccounts.ContainsKey("Facebook") ? currentUser.SocialAccounts["Facebook"] : "";
                instagramTextBox.Text = currentUser.SocialAccounts.ContainsKey("Instagram") ? currentUser.SocialAccounts["Instagram"] : "";
                xTextBox.Text = currentUser.SocialAccounts.ContainsKey("X") ? currentUser.SocialAccounts["X"] : "";
                tikTokTextBox.Text = currentUser.SocialAccounts.ContainsKey("TikTok") ? currentUser.SocialAccounts["TikTok"] : "";
            }

            Uri avatarUri = AvatarHelper.GetAvatarUriById(currentUser.PhotoId);
            if (avatarUri != null)
            {
                avatarEllipse.Fill = new ImageBrush { ImageSource = new BitmapImage(avatarUri) };
            }
        }


        private void ChooseAnAvatarButtonClick(object sender, RoutedEventArgs e)
        {
            var chooseAvatarWindow = new ChooseAvatarWindow();
            if (chooseAvatarWindow.ShowDialog() == true)
            {
                int newAvatarId = chooseAvatarWindow.selectedAvatarId;
                SessionManager.CurrentUser.PhotoId = newAvatarId;

                Uri avatarUri = AvatarHelper.GetAvatarUriById(newAvatarId);
                if (avatarUri != null)
                {
                    avatarEllipse.Fill = new ImageBrush { ImageSource = new BitmapImage(avatarUri) };
                }
            }
        }
        private void EditProfileButtonClick(object sender, RoutedEventArgs e)
        {
            SetEditMode(!isEditMode);
        }
        private void ButtonClickChangePassword(object sender, RoutedEventArgs e)
        {
            // Code to change password goes here
            MessageBox.Show("Change Password clicked!");
        }
        private async void SaveChangesButtonClick(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.IsUserLoggedIn())
            {
                return;
            }

            SetEditMode(false);
            saveChangesButton.IsEnabled = false;

            var updatedDto = CollectUserData();
            var client = new ProfileManagerClient();
            try
            {
                UserDTO resultDto = await client.UpdateProfileAsync(updatedDto);
                ProcessUpdateResponse(resultDto);
            }
            catch (Exception ex)
            {
                SetEditMode(true);
                MessageBox.Show("Error de conexión: " + ex.Message);

            }
            finally
            {
                client.Close();
            }
        }

        private void BackToLobbyButtonClick(object sender, RoutedEventArgs e)
        {
            if (isEditMode)
            {
                MessageBoxResult result = MessageBox.Show(
                    "Tienes cambios sin guardar. ¿Estás seguro de que quieres salir y descartarlos?",
                    "Descartar Cambios",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            if (NavigationService != null && NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }

        private void SetEditMode(bool isEnabled)
        {
            isEditMode = isEnabled;

            nicknameTextBox.IsEnabled = isEnabled;
            nameTextBox.IsEnabled = isEnabled;
            lastNameTextBox.IsEnabled = isEnabled;
            facebookTextBox.IsEnabled = isEnabled;
            instagramTextBox.IsEnabled = isEnabled;
            xTextBox.IsEnabled = isEnabled;
            tikTokTextBox.IsEnabled = isEnabled;

            chooseAnAvatarButton.IsEnabled = isEnabled;
            saveChangesButton.IsEnabled = isEnabled;
        }

        private UserDTO CollectUserData()
        {
            return new UserDTO
            {
                PlayerId = SessionManager.CurrentUser.PlayerId,
                Nickname = nicknameTextBox.Text,
                FirstName = nameTextBox.Text,
                LastName = lastNameTextBox.Text,
                PhotoId = SessionManager.CurrentUser.PhotoId,
                Email = SessionManager.CurrentUser.Email,
                SocialAccounts = new Dictionary<string, string>
                {
                    { "Facebook", facebookTextBox.Text },
                    { "Instagram", instagramTextBox.Text },
                    { "X", xTextBox.Text },
                    { "TikTok", tikTokTextBox.Text }
                }
            };
        }

        private void ProcessUpdateResponse(UserDTO resultDto)
        {
            if (resultDto != null)
            {
                // ÉXITO: Convertimos y actualizamos la sesión
                var updatedSessionUser = ConvertProfileDtoToSessionDto(resultDto);
                SessionManager.Login(updatedSessionUser);

                MessageBox.Show("¡Cambios guardados con éxito!", "Perfil Actualizado");
                if (NavigationService.CanGoBack) NavigationService.GoBack();
            }
            else
            {
                // FALLO LÓGICO: Volvemos al modo edición
                SetEditMode(true);
                MessageBox.Show("No se pudieron guardar los cambios en el servidor.", "Error");
            }
        }

        private SessionUserDTO ConvertProfileDtoToSessionDto(UserDTO profileDto)
        {
            var sessionDto = new SessionUserDTO
            {
                PlayerId = profileDto.PlayerId,
                Nickname = profileDto.Nickname,
                FirstName = profileDto.FirstName,
                LastName = profileDto.LastName,
                PhotoId = profileDto.PhotoId,
                Email = profileDto.Email,
                SocialAccounts = new Dictionary<string, string>()
            };
            if (profileDto.SocialAccounts != null)
            {
                foreach (var item in profileDto.SocialAccounts)
                {
                    sessionDto.SocialAccounts.Add(item.Key, item.Value);
                }
            }
            return sessionDto;
        }
    } 
}
