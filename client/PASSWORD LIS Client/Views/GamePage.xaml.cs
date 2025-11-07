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

namespace PASSWORD_LIS_Client.Views
{
    /// <summary>
    /// Lógica de interacción para GamePage.xaml
    /// </summary>
    public partial class GamePage : Page
    {
        public GamePage()
        {
            InitializeComponent();
            GameContentFrame.Navigated += GameContentFrameNavigated;
        }

        private void GameContentFrameNavigated(object sender, NavigationEventArgs e)
        {
            if (e.Content is Page page)
            {
                
                page.DataContext = this.DataContext;
            }
        }
    }
}
