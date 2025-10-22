using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.ViewModels;
using PASSWORD_LIS_Client.Views;
using System.Windows;

namespace PASSWORD_LIS_Client
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static BackgroundMusicService BackgroundMusicService { get; private set; }
        public static IWindowService WindowService { get; private set; }
        public static ILoginManagerService LoginManagerService { get; private set; }
        public static IAccountManagerService AccountManagerService { get; private set; }
        public static IFriendsManagerService FriendsManagerService { get; private set; }
        public static IPasswordResetManagerService PasswordResetManagerService { get; private set; }
        public static IProfileManagerService ProfileManagerService { get; private set; }
        public static ITopPlayersManagerService TopPlayersManagerService { get; private set; }
        public static IVerificationCodeManagerService VerificationCodeManagerService { get; private set; }
        public static IWaitingRoomManagerService WaitRoomManagerService { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Dependencies for LoginViewModel
            LoginManagerService = new WcfLoginManagerService();
            WindowService = new WindowService();
            // Other dependencies
            BackgroundMusicService = new BackgroundMusicService();
            AccountManagerService = new WcfAccountManagerService();
            FriendsManagerService = new WcfFriendsManagerService();
            PasswordResetManagerService = new WcfPasswordResetManagerService();
            ProfileManagerService = new WcfProfileManagerService();
            TopPlayersManagerService = new WcfTopPlayersManagerService();
            VerificationCodeManagerService = new WcfVerificationCodeManagerService();
            WaitRoomManagerService = new WcfWaitingRoomManagerService();
            

            var loginViewModel = new LoginViewModel(LoginManagerService, WindowService);
            var loginWindow = new LoginWindow {DataContext = loginViewModel};
            loginWindow.Show();

        }
    }
}
