using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using System.Windows.Input;


namespace PASSWORD_LIS_Client.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private double musicVolume;
        public double MusicVolume
        {
            get => musicVolume; 
            set { musicVolume = value; OnPropertyChanged();}
        }

        private double soundEffectsVolume;
        public double SoundEffectsVolume 
        { 
            get => soundEffectsVolume; 
            set { soundEffectsVolume = value; OnPropertyChanged(); } 
        }

        public ICommand LogoutCommand { get; }

        private readonly IWindowService windowService;
        private readonly IFriendsManagerService friendsManagerService;
        
        public bool WasLogoutSuccessful { get; private set; } = false;
        public SettingsViewModel(IWindowService windowService, IFriendsManagerService friendsManagerService) { 
            this.windowService = windowService;
            this.friendsManagerService = friendsManagerService;

            LogoutCommand = new RelayCommand(Logout);
        }

        private void Logout(object parameter)
        {
            if (SessionManager.IsUserLoggedIn()) //&& !SessionManager.CurrentUser. 
            {
                // Le decimos al servidor de amigos que nos vamos
                _ = friendsManagerService.UnsubscribeFromFriendUpdatesAsync(SessionManager.CurrentUser.UserAccountId);
            }
            SessionManager.Logout();
            WasLogoutSuccessful = true;
            windowService.CloseWindow(this);
        }
    }
}
