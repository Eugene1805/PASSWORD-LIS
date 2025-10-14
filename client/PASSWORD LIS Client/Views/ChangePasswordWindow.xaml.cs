using PASSWORD_LIS_Client.PasswordResetManagerServiceReference;
using PASSWORD_LIS_Client.Utils;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace PASSWORD_LIS_Client.Views
{
    /// <summary>
    /// Interaction logic for ChangePasswordWindow.xaml
    /// </summary>
    public partial class ChangePasswordWindow : Window
    {
        private readonly string accountEmail;
        private readonly string verificationCode;

        public ChangePasswordWindow()
        {
            InitializeComponent();
        }

        public ChangePasswordWindow(string email, string code) : this()
        {
            accountEmail = email;
            verificationCode = code;
        }

        private async void ButtonClickChangePassword(object sender, RoutedEventArgs e)
        {
            if (!IsInputValid())
            {
                return;
            }

            changePasswordButton.IsEnabled = false;
            Window popUpWindow;
            try
            {
                bool success = await TryResetPasswordOnServerAsync();

                if (success)
                {
                    ProcessSuccessfulPasswordChange();
                }
                else
                {
                    popUpWindow = new PopUpWindow(Properties.Langs.Lang.unexpectedErrorText,
                        Properties.Langs.Lang.passwordChangeFailedText,PopUpIcon.Warning);
                    popUpWindow.ShowDialog();
                }
            }
            catch (Exception)
            {
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
                popUpWindow.ShowDialog();
            }
            finally
            {
                changePasswordButton.IsEnabled = true;
            }
        }
        private async Task<bool> TryResetPasswordOnServerAsync()
        {
            var client = new PasswordResetManagerClient();
            Window popUpWindow;
            try
            {
                var passwordResetInfo = new PasswordResetDTO
                {
                    NewPassword = newPasswordBox.Password,
                    Email = this.accountEmail,
                    ResetCode = this.verificationCode
                };

                bool success = await client.ResetPasswordAsync(passwordResetInfo);

                client.Close();
                return success;
            }
            catch (TimeoutException)
            {
                client.Abort();
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.timeLimitTitleText,
                    Properties.Langs.Lang.serverTimeoutText, PopUpIcon.Warning);
                popUpWindow.ShowDialog();
                return false;
            }
            catch (System.ServiceModel.EndpointNotFoundException)
            {   
                client.Abort();
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.connectionErrorTitleText,
                    Properties.Langs.Lang.serverConnectionInternetErrorText, PopUpIcon.Warning);
                popUpWindow.ShowDialog();
                return false;
            }
            catch (System.ServiceModel.CommunicationException)
            {
                client.Abort();
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.networkErrorTitleText,
                    Properties.Langs.Lang.serverCommunicationErrorText, PopUpIcon.Warning);
                popUpWindow.ShowDialog();
                return false;
            }
            catch (Exception)
            {
                client.Abort();
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
                popUpWindow.ShowDialog();
                return false;
            }
        }
        private void ProcessSuccessfulPasswordChange()
        {
            Window popUpWindow = new PopUpWindow(Properties.Langs.Lang.succesfulPasswordChangeTitleText,
                Properties.Langs.Lang.successfulPasswordChangeText, PopUpIcon.Warning);
            popUpWindow.ShowDialog();
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
        private bool IsInputValid()
        {
            string newPassword = newPasswordBox.Password;
            string confirmPassword = confirmNewPasswordBox.Password;

            Window popUpWindow;
            if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.warningTitleText,
                    Properties.Langs.Lang.requiredFieldsText,PopUpIcon.Warning);
                popUpWindow.ShowDialog();
                return false;
            }

            if (!ValidationUtils.PasswordsMatch(newPassword,confirmPassword))
            {
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.warningTitleText,
                     Properties.Langs.Lang.matchingPasswordErrorText, PopUpIcon.Warning);
                popUpWindow.ShowDialog();
                return false;
            }

            return true;
        }
    }
}
