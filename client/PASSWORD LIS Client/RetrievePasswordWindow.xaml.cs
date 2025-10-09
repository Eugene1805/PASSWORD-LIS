using PASSWORD_LIS_Client.VerificationCodeManagerServiceReference;
using System;
using System.Windows;

namespace PASSWORD_LIS_Client
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
            var client = new AccountVerificationManagerClient();
            bool success = false;
            try
            {
                success = await client.ResendVerificationCodeAsync(emailTextBox.Text);
                client.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error de conexión al intentar reenviar el código: " + ex.Message);
                client?.Abort();
            }

            if (success)
            {
                MessageBox.Show("Se ha enviado un nuevo código a tu correo.");
            }
            else
            {
                MessageBox.Show("Por favor, espera al menos un minuto antes de solicitar otro código.");
            }
        }
    }
}
