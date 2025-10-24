using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ProfileUserDTO = PASSWORD_LIS_Client.ProfileManagerServiceReference.UserDTO;
using SessionUserDTO = PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class ProfileViewModel : BaseViewModel
    {
        private string nickname;
        public string Nickname
        {
            get => nickname;
            set { nickname = value; OnPropertyChanged(); }
        }

        private string firstName;
        public string FirstName
        {
            get => firstName;
            set { firstName = value; OnPropertyChanged(); }
        }

        private string lastName;
        public string LastName
        {
            get => lastName;
            set { lastName = value; OnPropertyChanged(); }
        }

        private int photoId;
        public int PhotoId
        {
            get => photoId;
            set { photoId = value; OnPropertyChanged(); }
        }

        private string facebook;
        public string Facebook
        {
            get => facebook;
            set { facebook = value; OnPropertyChanged(); }
        }

        private string instagram;
        public string Instagram
        {
            get => instagram;
            set { instagram = value; OnPropertyChanged(); }
        }

        private string xSocialMedia;
        public string XSocialMedia
        {
            get => xSocialMedia;
            set { xSocialMedia = value; OnPropertyChanged(); }
        }

        private string tiktok;
        public string Tiktok
        {
            get => tiktok;
            set { tiktok = value; OnPropertyChanged(); }
        }

        private bool isEditMode;
        public bool IsEditMode { 
            get => isEditMode; 
            set { isEditMode = value; OnPropertyChanged(); } 
        }

        private bool isSaving;
        public bool IsSaving { 
            get => isSaving; 
            set { isSaving = value; OnPropertyChanged(); } 
        }

        public ICommand BackToLobbyCommand { get; }
        public ICommand EditProfileCommand { get; }
        public ICommand ChooseAnAvatarCommand { get; }
        public ICommand SaveChangesCommand { get; }
        public ICommand ChangePasswordCommand { get; }

        private readonly IWindowService windowService;
        private readonly IProfileManagerService profileManagerClient;

        public ProfileViewModel(IProfileManagerService profileManagerService, IWindowService windowService)
        {
            this.profileManagerClient = profileManagerService;
            this.windowService = windowService;

            BackToLobbyCommand = new RelayCommand(BackToLobby);
            EditProfileCommand = new RelayCommand(EditProfile);
            ChooseAnAvatarCommand = new RelayCommand(ChooseAnAvatar);
            SaveChangesCommand = new RelayCommand(async (_) => await SaveChangesAsync(), (_) => CanSaveChanges());
            ChangePasswordCommand = new RelayCommand(ChangePassword);

            LoadProfileData();
        }

        private void LoadProfileData()
        {
            if (!SessionManager.IsUserLoggedIn())
            {
                return;
            }

            var currentUser = SessionManager.CurrentUser;
            Nickname = currentUser.Nickname;
            FirstName = currentUser.FirstName;
            LastName = currentUser.LastName;
            PhotoId = currentUser.PhotoId;

            if (currentUser.SocialAccounts != null)
            {
                Facebook = currentUser.SocialAccounts.ContainsKey("Facebook") ? currentUser.SocialAccounts["Facebook"] : "";
                Instagram = currentUser.SocialAccounts.ContainsKey("Instagram") ? currentUser.SocialAccounts["Instagram"] : "";
                XSocialMedia = currentUser.SocialAccounts.ContainsKey("X") ? currentUser.SocialAccounts["X"] : "";
                Tiktok = currentUser.SocialAccounts.ContainsKey("TikTok") ? currentUser.SocialAccounts["TikTok"] : "";
            }
        }

        private void EditProfile(object parameter)
        {
            IsEditMode = !IsEditMode;
        }

        private void ChooseAnAvatar(object parameter)
        {
            // Por simplicidad, la mantenemos aquí por ahora, pero también podría abstraerse a través del IWindowService si se vuelve más compleja.

            var chooseAvatarWindow = new ChooseAvatarWindow();
            if (chooseAvatarWindow.ShowDialog() == true)
            {
                PhotoId = chooseAvatarWindow.SelectedAvatarId;
            }
        }
        
        private bool CanSaveChanges()
        {
            return IsEditMode && !IsSaving;
        }

        private async Task SaveChangesAsync()
        {
            if (!AreFieldsValid() || !SessionManager.IsUserLoggedIn())
            {
                return;
            }

            IsSaving = true;

            try
            {
                var updatedDto = CollectUserData();
                var resultDto = await profileManagerClient.UpdateProfileAsync(updatedDto);
                ProcessUpdateResponse(resultDto);
            }
            catch (Exception)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.serverCommunicationErrorText,
                    PopUpIcon.Error);
                IsEditMode = true; 
            }
            finally
            {
                IsSaving = false;
            }
        }

        private void ChangePassword(object parameter)
        {
            var retrievePasswordViewModel = new RetrievePasswordViewModel(new WcfPasswordResetManagerService(), windowService);
            var retrievePasswordWindow = new RetrievePasswordWindow { DataContext = retrievePasswordViewModel};
            retrievePasswordWindow.ShowDialog();
        }

        private void BackToLobby(object parameter)
        {
            if (IsEditMode)
            {
                bool userConfirmedExit = windowService.ShowYesNoPopUp(
                    Properties.Langs.Lang.unsavedChangesWarningTitleText,
                    Properties.Langs.Lang.unsavedChangesWarningText);
               
                if (!userConfirmedExit)
                {
                    return;
                }
            }
            windowService.GoBack();
        }

        private bool AreFieldsValid()
        {
            string title = Properties.Langs.Lang.verificationFailedTitleText;

            if (string.IsNullOrWhiteSpace(FirstName))
            {
                windowService.ShowPopUp(title, Properties.Langs.Lang.emptyFirstNameText, PopUpIcon.Warning);
                return false;
            }
            if (FirstName.Length > 50)
            {
                windowService.ShowPopUp(title, Properties.Langs.Lang.firstNameTooLongText, PopUpIcon.Warning);
                return false;
            }
            if (!ValidationUtils.ContainsOnlyLetters(FirstName))
            {
                windowService.ShowPopUp(title, Properties.Langs.Lang.nameInvalidCharsText, PopUpIcon.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(LastName))
            {
                windowService.ShowPopUp(title, Properties.Langs.Lang.emptyLastNameText, PopUpIcon.Warning);
                return false;
            }
            if (LastName.Length > 80)
            {
                windowService.ShowPopUp(title, Properties.Langs.Lang.lastNameTooLongText, PopUpIcon.Warning);
                return false;
            }
            if (!ValidationUtils.ContainsOnlyLetters(LastName))
            {
                windowService.ShowPopUp(title, Properties.Langs.Lang.lastNameInvalidCharsText, PopUpIcon.Warning);
                return false;
            }
            return true;
        }

        private ProfileUserDTO CollectUserData()
        {
           return new ProfileUserDTO
           {
                PlayerId = SessionManager.CurrentUser.PlayerId,
                Email = SessionManager.CurrentUser.Email,
                PhotoId = this.PhotoId,
                Nickname = SessionManager.CurrentUser.Nickname,
                FirstName = this.FirstName,
                LastName = this.LastName,
                SocialAccounts = new Dictionary<string, string>()
                {
                    { "Facebook", this.Facebook },
                    { "Instagram", this.Instagram },
                    { "X", this.XSocialMedia },
                    { "TikTok", this.Tiktok }
                }
            };
        }

        private void ProcessUpdateResponse(ProfileUserDTO resultDto)
        {
            if (resultDto != null)
            {
                var updatedSessionUser = ConvertProfileDtoToSessionDto(resultDto);
                SessionManager.Login(updatedSessionUser);

                windowService.ShowPopUp(Properties.Langs.Lang.profileUpdatedTitleText,
                    Properties.Langs.Lang.profileChangesSavedSuccessText,
                    PopUpIcon.Success);

                IsEditMode = false; 
            }
            else
            {
                IsEditMode = true;
                windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.changesSavedErrorText,
                    PopUpIcon.Error);
            }
        }

        private SessionUserDTO ConvertProfileDtoToSessionDto(ProfileUserDTO profileDto)
        {
            var sessionDto = new SessionUserDTO
            {
                PlayerId = profileDto.PlayerId,
                Nickname = profileDto.Nickname,
                FirstName = profileDto.FirstName,
                LastName = profileDto.LastName,
                PhotoId = profileDto.PhotoId,
                Email = profileDto.Email,
                UserAccountId = SessionManager.CurrentUser.UserAccountId,
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