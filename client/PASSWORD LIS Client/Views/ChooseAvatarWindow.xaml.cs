using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PASSWORD_LIS_Client.Utils;

namespace PASSWORD_LIS_Client.Views
{
    /// <summary>
    /// Interaction logic for ChooseAvatarWindow.xaml
    /// </summary>
    public partial class ChooseAvatarWindow : Window
    {
        public int SelectedAvatarId { get; private set; } = 0;
        public ChooseAvatarWindow()
        {
            InitializeComponent();
        }

        private void SelectAvatarButtonClick(object sender, RoutedEventArgs e)
        {
            var checkedRadioButton = this.FindVisualChildren<RadioButton>()
                                                 .FirstOrDefault(rb => rb.IsChecked == true);

            if (checkedRadioButton != null)
            {
                SelectedAvatarId = int.Parse(checkedRadioButton.Tag.ToString());
                DialogResult = true;

            }
            else
            {
                DialogResult = false;
            }

            Close();
        }
    }
}
