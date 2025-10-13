using System;
using System.Windows;
using System.Windows.Media.Imaging;

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

        public PopUpWindow(string title, string message, PopUpIcon icon = PopUpIcon.Information) : this()
        {
            this.Title = title;
            messageTextBlock.Text = message;
            SetIcon(icon);
        }

        private void SetIcon(PopUpIcon icon)
        {
            string iconPath;
            switch (icon)
            {
                case PopUpIcon.Success:
                    iconPath = "/Resources/CorrectIcon.png";
                    break;
                case PopUpIcon.Warning:
                    iconPath = "/Resources/WarningIcon.png";
                    break;
                case PopUpIcon.Error:
                    iconPath = "/Resources/ErrorIcon.png";
                    break;
                default:
                    iconPath = "/Resources/InformationIcon.png";
                    break;
            }

            string packUri = $"pack://application:,,,{iconPath}";
            iconImage.Source = new BitmapImage(new Uri(packUri, UriKind.Absolute));
        }
    }
}
