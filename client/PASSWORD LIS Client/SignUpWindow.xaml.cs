using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PASSWORD_LIS_Client
{
    /// <summary>
    /// Interaction logic for SignUpWindow.xaml
    /// </summary>
    public partial class SignUpWindow : Window
    {
        private static readonly string tcUrl = "https://www.uv.mx/legislacion/files/2017/07/Codigo-de-etica-de-la-Universidad-Veracruzana.pdf";
        public SignUpWindow()
        {
                InitializeComponent();           
        }

        private void ButtonClickSignUp(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(emailTextBox.Text) || string.IsNullOrWhiteSpace(passwordBox.Password) || string.IsNullOrWhiteSpace(confirmPasswordBox.Password))
            {
                MessageBox.Show("Please fill in all fields.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (passwordBox.Password != confirmPasswordBox.Password)
            {
                MessageBox.Show("Passwords do not match.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            // Here you would typically add code to create the user account.
            // For this example, we'll just show a success message.
            MessageBox.Show("Account created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        private void HyperlinkClickLogIn(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        private void HyperlinkClickTC(object sender, RoutedEventArgs e)
        {
            Process.Start(tcUrl);
        }
    }
}
