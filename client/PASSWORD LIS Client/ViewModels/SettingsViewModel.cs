using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.Properties;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;


namespace PASSWORD_LIS_Client.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private readonly IWindowService windowService;
        private readonly IFriendsManagerService friendsManagerService;
        private readonly BackgroundMusicService backgroundMusicService;

        private double musicVolume;
        public double MusicVolume
        {
            get => musicVolume; 
            set
            {
                if (musicVolume != value)
                {
                    musicVolume = value;
                    backgroundMusicService.Volume = value;
                    OnPropertyChanged();
                }
            }
        }

        private double soundEffectsVolume;
        public double SoundEffectsVolume 
        { 
            get => soundEffectsVolume; 
            set { soundEffectsVolume = value; OnPropertyChanged(); } 
        }

        public ICommand LogoutCommand { get; }

        public bool WasLogoutSuccessful { get; private set; } = false;
        public SettingsViewModel(IWindowService windowService, IFriendsManagerService friendsManagerService,
            BackgroundMusicService backgroundMusicService) { 
            this.windowService = windowService;
            this.friendsManagerService = friendsManagerService;
            this.backgroundMusicService = backgroundMusicService;
            this.musicVolume = Settings.Default.MusicVolume;
            LogoutCommand = new RelayCommand(Logout);
        }
        private void Logout(object parameter)
        {
            backgroundMusicService.Stop();
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
