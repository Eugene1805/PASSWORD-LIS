using PASSWORD_LIS_Client.LoginManagerServiceReference;
using System;
using System.Windows;

namespace PASSWORD_LIS_Client
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        // --- MÉTODO DE LOGIN CORREGIDO Y MEJORADO ---
        private async void ButtonClickLogin(object sender, RoutedEventArgs e)
        {
            string email = emailTextBox.Text;
            string password = passwordBox.Password;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Por favor, ingresa tu email y contraseña.", "Campos vacíos");
                return;
            }

            // Deshabilitamos el botón para evitar clics múltiples

            // Creamos el cliente. Dejamos que WCF elija el endpoint por defecto desde App.config
            var client = new LoginManagerClient();
            try
            {
                // Usamos la llamada asíncrona para no congelar la aplicación
                UserDTO loggedInUser = await client.LoginAsync(email, password);

                if (loggedInUser != null)
                {
                    MessageBox.Show($"¡Bienvenido, {loggedInUser.Nickname}!", "Login Exitoso");

                    // Lógica de navegación CORRECTA:
                    var mainWindow = new MainWindow(); // O la ventana principal que deba abrirse
                    // Aquí le pasarías los datos del usuario a la nueva ventana si es necesario
                    // Ejemplo: ((App)Application.Current).CurrentUser = loggedInUser;
                    mainWindow.Show();
                    this.Close(); // Cerramos la ventana de login SOLO si el login es exitoso
                }
                else
                {
                    MessageBox.Show("Email o contraseña incorrectos.", "Error de Autenticación", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (System.ServiceModel.EndpointNotFoundException)
            {
                MessageBox.Show("No se pudo conectar al servidor. Asegúrate de que el servicio esté corriendo.", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ocurrió un error inesperado: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Este bloque se ejecuta siempre, haya error o no
                client.Close();
            }
            // Eliminamos la navegación incorrecta que estaba aquí
        }

        // --- MÉTODOS DE NAVEGACIÓN ADICIONALES (estos ya estaban bien) ---
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