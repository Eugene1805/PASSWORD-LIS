using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PASSWORD_LIS_Client.Utils;

namespace PASSWORD_LIS_Client.Views
{
    public partial class ChooseAvatarWindow : Window
    {
        private const int DefaultAvatarId = 0;
        public int SelectedAvatarId { get; private set; } 
        public ChooseAvatarWindow()
        {
            InitializeComponent();
            SelectedAvatarId = DefaultAvatarId;
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
