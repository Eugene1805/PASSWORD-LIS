using System.Windows.Controls;
using System.Windows.Navigation;

namespace PASSWORD_LIS_Client.Views
{
    public partial class GamePage : Page
    {
        public GamePage()
        {
            InitializeComponent();
            gameContentFrame.Navigated += GameContentFrameNavigated;
        }

        private void GameContentFrameNavigated(object sender, NavigationEventArgs events)
        {
            if (events.Content is Page page)
            {
                page.DataContext = this.DataContext;
            }
        }
    }
}
