using PASSWORD_LIS_Client.LoginManagerServiceReference;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System;
using System.Collections.Generic;
using System.Windows;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.ViewModels;

namespace PASSWORD_LIS_Client
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private async void ButtonClickLogin(object sender, RoutedEventArgs e)
        {
            if (!AreFieldsValid())
            {
                return; 
            }
            
            var client = new LoginManagerClient();
            try
            {
                UserDTO loggedInUser = await client.LoginAsync(emailTextBox.Text, passwordBox.Password);

                if (loggedInUser != null)
                {
                    SessionManager.Login(loggedInUser);

                    new PopUpWindow(Properties.Langs.Lang.successfulLoginText,
                        string.Format(Properties.Langs.Lang.loginWelcomeText, SessionManager.CurrentUser.Nickname),
                        PopUpIcon.Success).ShowDialog();

                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                    this.Close();
                }
                else
                {
                    new PopUpWindow(Properties.Langs.Lang.warningTitleText,
                        Properties.Langs.Lang.wrongCredentialsText,
                        PopUpIcon.Warning).ShowDialog();    
                }
                client.Close();
            }
            catch (Exception ex)
            {
                new PopUpWindow(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.serverCommunicationErrorText,
                    PopUpIcon.Error).ShowDialog();

                client.Abort();
            }
        }

        private bool AreFieldsValid()
        {
            if (string.IsNullOrWhiteSpace(emailTextBox.Text) || string.IsNullOrWhiteSpace(passwordBox.Password))
            {
                new PopUpWindow(Properties.Langs.Lang.warningTitleText,
                    Properties.Langs.Lang.requiredEmailAndPassWordText,
                    PopUpIcon.Warning).ShowDialog();
                return false;
            }
            return true;
        }
        private void HyperlinkClickForgotPassword(object sender, RoutedEventArgs e)
        {
            var retrievePasswordWindow = new RetrievePasswordWindow();
            retrievePasswordWindow.ShowDialog();
        }

        private void HyperlinkClickSignUp(object sender, RoutedEventArgs e)
        {
            var signUpViewModel = new SignUpViewModel(new WcfAccountManagerService(), new WindowService());
            var signUpWindow = new SignUpWindow { DataContext = signUpViewModel};
            signUpWindow.Show();
            this.Close();
        }

        private void PlayAsGuestButtonClick(object sender, RoutedEventArgs e)
        {
            string guestNickname = Properties.Langs.Lang.guestText + new Random().Next(1000, 9999);

            var guestUser = new UserDTO
            {
                PlayerId = -1, 
                Nickname = guestNickname,
                Email = string.Empty,
                FirstName = Properties.Langs.Lang.guestText,
                LastName = string.Empty,
                PhotoId = 0, 
                SocialAccounts = new Dictionary<string, string>() 
            };

            SessionManager.Login(guestUser);

            new PopUpWindow(
                Properties.Langs.Lang.successfulLoginText,
                string.Format(Properties.Langs.Lang.loginWelcomeText, guestUser.Nickname),
                PopUpIcon.Success
            ).ShowDialog();

            var mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }
    }
}