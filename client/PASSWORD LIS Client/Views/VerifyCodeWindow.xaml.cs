using PASSWORD_LIS_Client.VerificationCodeManagerServiceReference;
using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;

namespace PASSWORD_LIS_Client.Views
{
    /// <summary>
    /// Interaction logic for VerifyCodeWindow.xaml
    /// </summary>

    public enum VerificationReason
    {
        AccountActivation,
        PasswordReset
    }
    public partial class VerifyCodeWindow : Window
    {
        public string EnteredCode { get; private set; }
        private readonly string email;
        private readonly VerificationReason reason;
        public VerifyCodeWindow()
        {
            InitializeComponent();
        }

        public VerifyCodeWindow(string email, VerificationReason reason) : this()
        {
            emailTextblock.Text = email;
            this.reason = reason;
            this.email = email;
        }

        private async void ButtonClickVerifyCode(object sender, RoutedEventArgs e)
        {
            string code = verificationCodeTextBox.Text;
            Window popUpWindow;
            if (string.IsNullOrWhiteSpace(code))
            {
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.codeWordText,
                            Properties.Langs.Lang.requiredFieldsText, PopUpIcon.Warning);
                popUpWindow.ShowDialog();
                return;
            }

            verifyCodeButton.IsEnabled = false;
            resendCodeHyperlink.IsEnabled = false; 

            bool isCodeValid = await TryVerifyCodeAsync(code);

            if (isCodeValid)
            {
                this.EnteredCode = code;
                this.DialogResult = true; 
            }
            else
            {
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.verificationFailedTitleText,
                            Properties.Langs.Lang.codeIncorrectOrExpiredText, PopUpIcon.Error);
                popUpWindow.ShowDialog();
                verifyCodeButton.IsEnabled = true;
                resendCodeHyperlink.IsEnabled = true;
            }
        }

        private async void HyperlinkClickResendCode(object sender, RoutedEventArgs e)
        {
            verifyCodeButton.IsEnabled = false;
            resendCodeHyperlink.IsEnabled = false;

            bool success = await TryResendCodeAsync();
            Window popUpWindow;
            if (success)
            {
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.codeSentTitleText,
                            Properties.Langs.Lang.newCodeSentText, PopUpIcon.Information);
                popUpWindow.ShowDialog();
            }
            else
            {
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.timeLimitTitleText,
                            Properties.Langs.Lang.waitAMinuteForCodeText, PopUpIcon.Warning);
                popUpWindow.ShowDialog();
            }

            verifyCodeButton.IsEnabled = true;
            resendCodeHyperlink.IsEnabled = true;
        }
        
        private async Task<bool> TryVerifyCodeAsync(string code)
        {
            ICommunicationObject client = null;
            Window popUpWindow;
            try
            {
                bool isValid = false;
                switch (reason)
                {
                    case VerificationReason.AccountActivation:
                        var activationClient = new AccountVerificationManagerClient();
                        client = activationClient;
                        var dto = new EmailVerificationDTO { Email = this.email, VerificationCode = code };
                        isValid = await activationClient.VerifyEmailAsync(dto);
                        break;

                    case VerificationReason.PasswordReset:
                        var resetClient = new PasswordResetManagerServiceReference.PasswordResetManagerClient();
                        client = resetClient; 
                        var resetDto = new PasswordResetManagerServiceReference.EmailVerificationDTO { Email = this.email, VerificationCode = code };
                        isValid = await resetClient.ValidatePasswordResetCodeAsync(resetDto);
                        break;
                }
                client.Close();
                return isValid;
            }
            catch (TimeoutException)
            {
                client?.Abort();
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.timeLimitTitleText,
                            Properties.Langs.Lang.serverTimeoutText,PopUpIcon.Warning);
                popUpWindow.ShowDialog(); return false;
            }
            catch (EndpointNotFoundException)
            {
                client?.Abort();
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.connectionErrorTitleText,
                            Properties.Langs.Lang.serverConnectionInternetErrorText, PopUpIcon.Warning);
                popUpWindow.ShowDialog();
                return false;
            }
            catch (Exception)
            {
                client?.Abort();
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.errorTitleText,
                            Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
                popUpWindow.ShowDialog();
                return false;
            }
        }

        private async Task<bool> TryResendCodeAsync()
        {
            ICommunicationObject client = null;
            Window popUpWindow;
            try
            {
                bool success = false;
                switch (reason)
                {
                    case VerificationReason.AccountActivation:
                        var activationClient = new AccountVerificationManagerClient();
                        client = activationClient;
                        success = await activationClient.ResendVerificationCodeAsync(this.email);
                        break;
                    case VerificationReason.PasswordReset:
                        var resetClient = new PasswordResetManagerServiceReference.PasswordResetManagerClient();
                        client = resetClient;
                        var resetDto = new PasswordResetManagerServiceReference.EmailVerificationDTO { Email = this.email, VerificationCode = "" };
                        success = await resetClient.RequestPasswordResetCodeAsync(resetDto);
                        break;
                }
                client.Close();
                return success;
            }
            catch (TimeoutException)
            {
                client?.Abort();
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.timeLimitTitleText,
                                                    Properties.Langs.Lang.serverTimeoutText, PopUpIcon.Warning);
                popUpWindow.ShowDialog(); 
                return false;
            }
            catch (EndpointNotFoundException)
            {
                client?.Abort();
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.connectionErrorTitleText,
                                    Properties.Langs.Lang.serverConnectionInternetErrorText, PopUpIcon.Warning);
                popUpWindow.ShowDialog();
                return false;
            }
            catch (Exception)
            {
                client?.Abort();
                popUpWindow = new PopUpWindow(Properties.Langs.Lang.errorTitleText,
                                                    Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
                popUpWindow.ShowDialog();
                return false;
            }
        }
    }
}
