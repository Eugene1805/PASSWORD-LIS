using PASSWORD_LIS_Client.LoginManagerServiceReference;
using System;
using System.Windows;
using PASSWORD_LIS_Client.Utils;

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
                    
                    MessageBox.Show(string.Format(Properties.Langs.Lang.loginWelcomeText, loggedInUser.Nickname),
                                    Properties.Langs.Lang.successfulLoginText, MessageBoxButton.OK);

                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                    this.Close(); 
                }
                else
                {
                    MessageBox.Show(Properties.Langs.Lang.wrongCredentialsText,
                                    "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (System.ServiceModel.EndpointNotFoundException)
            {
                MessageBox.Show(Properties.Langs.Lang.loginConnectionErrorText,
                                "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(string.Format(Properties.Langs.Lang.loginUnexpectedErrorText, ex.Message),
                                "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                client.Close();
            }
        }

        private bool AreFieldsValid()
        {
            if (string.IsNullOrWhiteSpace(emailTextBox.Text) || string.IsNullOrWhiteSpace(passwordBox.Password))
            {
                MessageBox.Show(Properties.Langs.Lang.requiredEmailAndPassWordText,
                                Properties.Langs.Lang.warningTitleText, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }
        private void HyperlinkClickForgotPassword(object sender, RoutedEventArgs e)
        {
            var retrievePasswordWindow = new RetrievePasswordWindow();
            retrievePasswordWindow.Show();
            this.Close();
        }

        private void HyperlinkClickSignUp(object sender, RoutedEventArgs e)
        {
            var signUpWindow = new SignUpWindow();
            signUpWindow.Show();
            this.Close();
        }
    }
}