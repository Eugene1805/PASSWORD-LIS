using PASSWORD_LIS_Client.PasswordResetManagerServiceReference;
using PASSWORD_LIS_Client.Utils;
using System;
using System.ServiceModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace PASSWORD_LIS_Client.Views
{
    /// <summary>
    /// Interaction logic for RetrievePasswordWindow.xaml
    /// </summary>
    public partial class RetrievePasswordWindow : Window
    {
        public RetrievePasswordWindow()
        {
            InitializeComponent();
        }

        private async void ButtonClickSendCode(object sender, RoutedEventArgs e)
        {
            if (!IsInputValid())
            {
                return;
            }
            Window popUpWindow;
            sendCodeButton.IsEnabled = false;
            try
            {
                bool success = await TryRequestResetCodeAsync(emailTextBox.Text);

                if (success)
                {
                    ProcessCodeRequestSuccess(emailTextBox.Text);
                }
                else
                {
                    popUpWindow = new PopUpWindow(Properties.Langs.Lang.sendFailedTitleText,
                        Properties.Langs.Lang.codeSendFailedText,PopUpIcon.Error);
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
                sendCodeButton.IsEnabled = true;
            }
        }

        private async Task<bool> TryRequestResetCodeAsync(string email)
        {
            var client = new PasswordResetManagerClient();
            Window popUpWindow;
            try
            {
                var requestDto = new EmailVerificationDTO { Email = email, VerificationCode = "" };
                bool success = await client.RequestPasswordResetCodeAsync(requestDto);
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
            catch (EndpointNotFoundException)
            {
                client.Abort();
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.connectionErrorTitleText,
                    Properties.Langs.Lang.serverConnectionInternetErrorText , PopUpIcon.Warning );
                popUpWindow.ShowDialog();
                return false;
            }
            catch (CommunicationException)
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

        private void ProcessCodeRequestSuccess(string email)
        {
            var verifyCodeWindow = new VerifyCodeWindow(email, VerificationReason.PasswordReset);
            bool? result = verifyCodeWindow.ShowDialog();

            if (result == true)
            {
                var changePasswordWindow = new ChangePasswordWindow(email, verifyCodeWindow.EnteredCode);
                changePasswordWindow.Show();
                this.Close();
            }
        }

        private bool IsInputValid()
        {
            Window popUpWindow;
            string email = emailTextBox.Text;
            if (string.IsNullOrWhiteSpace(email))
            {
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.warningTitleText,
                    Properties.Langs.Lang.requiredFieldsText, PopUpIcon.Warning);
                popUpWindow.ShowDialog();
                return false;
            }

            if(!ValidationUtils.IsValidEmail(email))
            {
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.warningTitleText,
                    Properties.Langs.Lang.invalidEmailErrorText, PopUpIcon.Warning);
                popUpWindow.ShowDialog();
                return false;
            }
            return true;
        }
    }
}
