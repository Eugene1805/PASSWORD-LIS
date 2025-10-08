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

        private void ButtonClickSendCode(object sender, RoutedEventArgs e)
        {
            // Code to send code goes here
            MessageBox.Show("Code sent to your email!");
        }
    }
}
