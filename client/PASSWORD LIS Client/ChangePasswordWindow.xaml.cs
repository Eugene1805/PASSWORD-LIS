using PASSWORD_LIS_Client.PasswordResetManagerServiceReference;
using System;
using System.Windows;
using System.Windows.Controls;

namespace PASSWORD_LIS_Client
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

        private void ButtonClickChangePassword(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(newPasswordBox.Password) || string.IsNullOrWhiteSpace(confirmNewPasswordBox.Password))
            {
                MessageBox.Show("Ambos campos de contraseña son requeridos.", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (newPasswordBox.Password != confirmNewPasswordBox.Password)
            {
                MessageBox.Show("Las contraseñas no coinciden.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            changePasswordButton.IsEnabled = false;
            var client = new PasswordResetManagerClient();
            bool success = false;
            try
            {
                success = client.ResetPassword(new PasswordResetDTO
                {
                    NewPassword = newPasswordBox.Password,
                    Email = accountEmail,
                    ResetCode = verificationCode
                });
            }
            catch (Exception ex) {
                MessageBox.Show("Ocurrió un error al cambiar la contraseña: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                client.Abort();
            }
            finally
            {
                changePasswordButton.IsEnabled = true;
            }
            
            if (success) {
                MessageBox.Show("Contraseña cambiada exitosamente. Ahora serás redirigido al inicio de sesión.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                var loginWindow = new LoginWindow();
                loginWindow.Show();
                this.Close();
            }
            else
            {
                MessageBox.Show("No se pudo cambiar la contraseña");
            }
            

        }
    }
}
