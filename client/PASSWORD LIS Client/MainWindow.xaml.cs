using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.ViewModels;
using PASSWORD_LIS_Client.Views;
using System.Windows;
using System.Windows.Input;

namespace PASSWORD_LIS_Client
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            (App.WindowService as WindowService)?.Initialize(mainFrame);
            SetInitialPage();
        }

        public void SetInitialPage()
        {
            if (SessionManager.IsUserLoggedIn())
            {
                var lobbyViewModel = new LobbyViewModel(App.WindowService, App.FriendsManagerService, 
                    App.WaitRoomManagerService, App.ReportManagerService);

                var lobbyPage = new LobbyPage { DataContext = lobbyViewModel };

                App.WindowService.NavigateTo(lobbyPage);
            }
            else
            {
                var loginViewModel = new LoginViewModel(App.LoginManagerService, App.WindowService);
                var loginWindow = new LoginWindow { DataContext = loginViewModel };
                loginWindow.Show();
                this.Close();
            }

        }

        private void MainFramePreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.BrowserBack || e.Key == Key.BrowserForward)
            {
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Back)
            {
                var focusedElement = Keyboard.FocusedElement;
                
                bool isInTextInput = focusedElement is System.Windows.Controls.TextBox ||
                                     focusedElement is System.Windows.Controls.PasswordBox ||
                                     focusedElement is System.Windows.Controls.RichTextBox ||
                                     focusedElement is System.Windows.Controls.Primitives.TextBoxBase;

                if (!isInTextInput)
                {
                    e.Handled = true;
                }
            }
        }
    }
}
