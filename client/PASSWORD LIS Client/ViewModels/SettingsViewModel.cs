using PASSWORD_LIS_Client.Commands;
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
        
        public bool WasLogoutSuccessful { get; private set; } = false;
        public SettingsViewModel(IWindowService windowService) { 
            this.windowService = windowService;

            LogoutCommand = new RelayCommand(Logout);
        }

        private void Logout(object parameter)
        {
            SessionManager.Logout();
            WasLogoutSuccessful = true;
            windowService.CloseWindow(this);
        }
    }
}
