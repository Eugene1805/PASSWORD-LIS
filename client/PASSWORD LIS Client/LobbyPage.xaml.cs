using PASSWORD_LIS_Client.LoginManagerServiceReference;
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
    /// Lógica de interacción para LobbyPage.xaml
    /// </summary>
    public partial class LobbyPage : Page
    {
        private readonly UserDTO currentUser;
        public LobbyPage(UserDTO user)
        {
            InitializeComponent();
            currentUser = user;

            LoadUserProfile();
        }

        private void LoadUserProfile()
        {
            if (currentUser == null)
            {
                return;
            }

            //string avatarPath = GetAvatharP
        }
    }
}
