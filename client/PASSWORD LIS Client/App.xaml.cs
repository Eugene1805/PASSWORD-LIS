using log4net;
using log4net.Config;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.ViewModels;
using PASSWORD_LIS_Client.Views;
using System.Globalization;
using System.Net.NetworkInformation;
using System.ServiceModel;
using System.Threading;
using System.Windows;

namespace PASSWORD_LIS_Client
{
    
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
        public static IReportManagerService ReportManagerService { get; private set; }
        public static IGameManagerService GameManagerService { get; private set; }
        private static readonly ILog log = LogManager.GetLogger(typeof(App));

        public static void ResetServices()
        {
            log.Info("Resetting all WCF services due to connection loss...");

            foreach (var service in GetAllServiceInstances())
            {
                CloseServiceSafely(service);
            }

            InitializeAllServices();

            log.Info("Services successfully reset.");
        }

        private static void InitializeAllServices()
        {
            LoginManagerService = new WcfLoginManagerService();
            WindowService = new WindowService();
            BackgroundMusicService = new BackgroundMusicService();
            AccountManagerService = new WcfAccountManagerService();
            FriendsManagerService = new WcfFriendsManagerService();
            PasswordResetManagerService = new WcfPasswordResetManagerService();
            ProfileManagerService = new WcfProfileManagerService();
            TopPlayersManagerService = new WcfTopPlayersManagerService();
            VerificationCodeManagerService = new WcfVerificationCodeManagerService();
            WaitRoomManagerService = new WcfWaitingRoomManagerService();
            ReportManagerService = new WcfReportManagerService();
            GameManagerService = new WcfGameManagerService();
        }

        private static object[] GetAllServiceInstances()
        {
            return new object[]
            {
                LoginManagerService,
                AccountManagerService,
                FriendsManagerService,
                PasswordResetManagerService,
                ProfileManagerService,
                TopPlayersManagerService,
                VerificationCodeManagerService,
                WaitRoomManagerService,
                ReportManagerService,
                GameManagerService
            };
        }

        private static void CloseServiceSafely(object service)
        {
            if (service is ICommunicationObject commObj)
            {
                try
                {
                    commObj.Close();
                }
                catch
                {
                    commObj.Abort();
                }
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            XmlConfigurator.Configure();
            base.OnStartup(e);

            var lang = PASSWORD_LIS_Client.Properties.Settings.Default.languageCode;

            if (!string.IsNullOrEmpty(lang))
            {
                var culture = new CultureInfo(lang);
                TranslationProvider.Instance.SetLanguage(culture);
                Thread.CurrentThread.CurrentUICulture = culture;
            }

            InitializeAllServices();

            var loginViewModel = new LoginViewModel(LoginManagerService, WindowService);
            var loginWindow = new LoginWindow { DataContext = loginViewModel };
            loginWindow.Show();

            log.Info("App started");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            BackgroundMusicService?.Dispose();
            base.OnExit(e);
        }
    }
}
