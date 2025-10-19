using PASSWORD_LIS_Client.Utils;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class MainWindowViewModel : BaseViewModel
    {
        private readonly IWindowService windowService;

        public MainWindowViewModel(IWindowService windowService)
        {
            this.windowService = windowService;

            Messenger.UserLoggedOut += OnUserLoggedOut;
        }

        private void OnUserLoggedOut()
        {
            Messenger.Unsubscribe(OnUserLoggedOut);
            windowService.ShowLoginWindow();
            windowService.CloseWindow(this);
        }
    }

    
  
}
