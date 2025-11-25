using PASSWORD_LIS_Client.AccountManagerServiceReference;
using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;
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
            set { firstName = value; ValidateFirstName(); OnPropertyChanged(); }
        }

        private string lastName;
        public string LastName
        {
            get => lastName;
            set { lastName = value; ValidateLastName(); OnPropertyChanged(); }
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
            set { facebook = value; ValidateFacebook(); OnPropertyChanged(); }
        }

        private string instagram;
        public string Instagram
        {
            get => instagram;
            set { instagram = value; ValidateInstagram(); OnPropertyChanged(); }
        }

        private string xSocialMedia;
        public string XSocialMedia
        {
            get => xSocialMedia;
            set { xSocialMedia = value; ValidateXSocialMedia(); OnPropertyChanged(); }
        }

        private string tiktok;
        public string Tiktok
        {
            get => tiktok;
            set { tiktok = value; ValidateTiktok(); OnPropertyChanged(); }
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
        private string firstNameError;
        public string FirstNameError
        {
            get => firstNameError;
            set { firstNameError = value; OnPropertyChanged(); }
        }
        private string lastNameError;
        public string LastNameError
        {
            get => lastNameError;
            set { lastNameError = value; OnPropertyChanged(); }
        }
        private string facebookError;
        public string FacebookError
        {
            get => facebookError;
            set { facebookError = value; OnPropertyChanged(); }
        }
        private string instagramError;
        public string InstagramError
        {
            get => instagramError;
            set { instagramError = value; OnPropertyChanged(); }
        }
        private string xSocialMediaError;
        public string XSocialMediaError
        {
            get => xSocialMediaError;
            set { xSocialMediaError = value; OnPropertyChanged(); }
        }
        private string tiktokError;
        public string TiktokError
        {
            get => tiktokError;
            set { tiktokError = value; OnPropertyChanged(); }
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
            IsEditMode = true; 
            ClearAllErrors();

            windowService.ShowPopUp("Edicion",
                "Edicion Activada", PopUpIcon.Information); //Properties.Langs.Lang.editingModeTitle, Properties.Langs.Lang.editingModeActiveText
            
        }

        private void ChooseAnAvatar(object parameter)
        {
            var chooseAvatarWindow = new ChooseAvatarWindow();
            if (chooseAvatarWindow.ShowDialog() == true)
            {
                PhotoId = chooseAvatarWindow.SelectedAvatarId;
            }
        }
        
        private bool CanSaveChanges()
        {
            return IsEditMode && !IsSaving &&
                   !string.IsNullOrWhiteSpace(FirstName) &&
                   !string.IsNullOrWhiteSpace(LastName);
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
            catch (FaultException<ServiceErrorDetailDTO> ex) 
            {
                windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                                        ex.Detail.Message, PopUpIcon.Error);
                IsEditMode = true; 
            }
            catch (TimeoutException)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.timeLimitTitleText,
                                        Properties.Langs.Lang.serverTimeoutText, PopUpIcon.Warning);
                IsEditMode = true;
            }
            catch (EndpointNotFoundException) 
            {
                windowService.ShowPopUp(Properties.Langs.Lang.connectionErrorTitleText,
                                        Properties.Langs.Lang.serverConnectionInternetErrorText, PopUpIcon.Error);
                IsEditMode = true;
            }
            catch (CommunicationException)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.networkErrorTitleText,
                                        Properties.Langs.Lang.serverCommunicationErrorText, PopUpIcon.Error);
                IsEditMode = true;
            }
            catch (Exception)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.unexpectedErrorText,
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
            var retrievePasswordViewModel = new RetrievePasswordViewModel(App.PasswordResetManagerService, windowService);
            var retrievePasswordWindow = new RetrievePasswordWindow { DataContext = retrievePasswordViewModel};
            retrievePasswordWindow.Show();
        }

        private void BackToLobby(object parameter)
        {
            if (IsEditMode)
            {
                windowService.ShowPopUp("Modo edición Activado", "Debe guardar sus cambios para poder volver al lobby",
                    PopUpIcon.Information);
                return;
            }
            windowService.GoBack();
        }
        private void ClearAllErrors()
        {
            FirstNameError = null;
            LastNameError = null;
            FacebookError = null;
            InstagramError = null;
            XSocialMediaError = null;
            TiktokError = null;
        }

        private bool AreFieldsValid()
        {
            bool isFirstNameValid = ValidateFirstName();
            bool isLastNameValid = ValidateLastName();
            bool isFacebookValid = ValidateFacebook();
            bool isInstagramValid = ValidateInstagram();
            bool isXValid = ValidateXSocialMedia();
            bool isTiktokValid = ValidateTiktok();

            
            return isFirstNameValid && isLastNameValid && isFacebookValid && isInstagramValid && isXValid && isTiktokValid;
        }

        private bool ValidateFirstName()
        {
            if (string.IsNullOrWhiteSpace(FirstName))
            {
                FirstNameError = Properties.Langs.Lang.emptyFirstNameText;
                return false;
            }
            if (FirstName.Length > 50)
            {
                FirstNameError = Properties.Langs.Lang.firstNameTooLongText;
                return false;
            }
            if (!ValidationUtils.ContainsOnlyLetters(FirstName))
            {
                FirstNameError = Properties.Langs.Lang.nameInvalidCharsText;
                return false;
            }
            FirstNameError = null; // Limpia el error si es válido
            return true; ;
        }

        private bool ValidateLastName()
        {
            if (string.IsNullOrWhiteSpace(LastName))
            {
                LastNameError = Properties.Langs.Lang.emptyLastNameText;
                return false;
            }
            if (LastName.Length > 80)
            {
                LastNameError = Properties.Langs.Lang.lastNameTooLongText;
                return false;
            }
            if (!ValidationUtils.ContainsOnlyLetters(LastName))
            {
                LastNameError = Properties.Langs.Lang.lastNameInvalidCharsText;
                return false;
            }
            LastNameError = null; // Limpia el error si es válido
            return true;
        }
        private string ValidateSocialMediaField(string socialMediaUsername)
        {
            if (!string.IsNullOrEmpty(socialMediaUsername) && socialMediaUsername.Length > 50)
            {
                return "El usuario no debe exceder los 50 caracteres."; // O usa un Lang resource
            }
            return null;
        }
        private bool ValidateFacebook()
        {
            FacebookError = ValidateSocialMediaField(Facebook);
            return FacebookError == null;
        }

        private bool ValidateInstagram()
        {
            InstagramError = ValidateSocialMediaField(Instagram);
            return InstagramError == null;
        }

        private bool ValidateXSocialMedia()
        {
            XSocialMediaError = ValidateSocialMediaField(XSocialMedia);
            return XSocialMediaError == null;
        }

        private bool ValidateTiktok()
        {
            TiktokError = ValidateSocialMediaField(Tiktok);
            return TiktokError == null;
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
                ClearAllErrors();
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