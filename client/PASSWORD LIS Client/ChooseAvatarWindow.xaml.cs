using System;
using System.Collections.Generic;
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
using PASSWORD_LIS_Client.Utils;

namespace PASSWORD_LIS_Client
{
    /// <summary>
    /// Interaction logic for ChooseAvatarWindow.xaml
    /// </summary>
    public partial class ChooseAvatarWindow : Window
    {
        public int selectedAvatarId { get; private set; } = 0;


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
                selectedAvatarId = int.Parse(checkedRadioButton.Tag.ToString());
                this.DialogResult = true;

            }
            else
            {
                this.DialogResult = false;
            }

            this.Close();
        }
    }
}
