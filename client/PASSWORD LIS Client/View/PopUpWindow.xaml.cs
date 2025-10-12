using System.Windows;

namespace PASSWORD_LIS_Client.View
{
    /// <summary>
    /// Interaction logic for PopUpWindow.xaml
    /// </summary>
    public partial class PopUpWindow : Window
    {
        public PopUpWindow()
        {
            InitializeComponent();
        }
        public PopUpWindow(string title, string message) : this()
        {   
            this.Title = title;
            MessageTextBlock.Text = message;
        }
    }
}
