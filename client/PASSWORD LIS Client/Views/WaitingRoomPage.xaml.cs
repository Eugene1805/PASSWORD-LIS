using PASSWORD_LIS_Client.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace PASSWORD_LIS_Client.Views
{
    /// <summary>
    /// Lógica de interacción para WaitingRoomPage.xaml
    /// </summary>
    public partial class WaitingRoomPage : Page
    {
        private readonly string username;
        private readonly bool isGuest;
        public WaitingRoomPage()
        {
            InitializeComponent();
            this.Loaded += WaitingRoomPage_Loaded;
        }
        public WaitingRoomPage(string username, bool isGuest)
        {
            InitializeComponent();
            this.username = username;
            this.isGuest = isGuest;
            this.Loaded += WaitingRoomPage_Loaded;
        }
        private async void WaitingRoomPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is WaitingRoomViewModel viewModel)
            {
                await viewModel.LoadInitialDataAsync(username,isGuest);
            }
        }
    }
}
