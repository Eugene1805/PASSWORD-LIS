using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class MainWindowViewModel : BaseViewModel
    {
        private readonly IWindowService windowService;
        private readonly BackgroundMusicService musicService;
        private double volume;

        public ICommand PlayCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand StopCommand { get; }

        public double Volume
        {
            get => volume;
            set
            {
                if (volume != value)
                {
                    volume = value;
                    musicService.Volume = volume;
                    OnPropertyChanged();
                }
            }
        }

        public MainWindowViewModel(IWindowService windowService, BackgroundMusicService backgroudMusicService)
        {
            this.windowService = windowService;
            this.musicService = backgroudMusicService;
            Messenger.UserLoggedOut += OnUserLoggedOut;
            this.volume = musicService.Volume;

            PlayCommand = new RelayCommand(_ => musicService.Play());
            PauseCommand = new RelayCommand(_ => musicService.Pause());
            StopCommand = new RelayCommand(_ => musicService.Stop());

            musicService.Play();
        }

        private void OnUserLoggedOut()
        {
            Messenger.Unsubscribe(OnUserLoggedOut);
            windowService.ShowLoginWindow();
            windowService.CloseWindow(this);
        }

        public void OnClosed()
        {
            musicService.Dispose();
        }

    }

}
