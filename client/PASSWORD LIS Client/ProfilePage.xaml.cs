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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PASSWORD_LIS_Client
{
    /// <summary>
    /// Interaction logic for ProfilePage.xaml
    /// </summary>
    public partial class ProfilePage : Page
    {
        public ProfilePage()
        {
            InitializeComponent();
        }

        private void ButtonClickChooseAnAvatar(object sender, RoutedEventArgs e)
        {
            var chooseAvatarWindow = new ChooseAvatarWindow();
            chooseAvatarWindow.ShowDialog();
        }
        private void ButtonClickEditProfile(object sender, RoutedEventArgs e)
        {
            // Code to edit profile goes here
            MessageBox.Show("Edit Profile clicked!");
        }
        private void ButtonClickChangePassword(object sender, RoutedEventArgs e)
        {
            // Code to change password goes here
            MessageBox.Show("Change Password clicked!");
        }
        private void ButtonClickSaveChanges(object sender, RoutedEventArgs e)
        {
            // Code to save changes goes here
            MessageBox.Show("Changes saved successfully!");
        }
    }
}
