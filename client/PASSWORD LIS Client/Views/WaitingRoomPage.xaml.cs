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
        public WaitingRoomPage()
        {
            Console.WriteLine("Se intento inicializar el page");
            InitializeComponent();
            this.Loaded += WaitingRoomPage_Loaded;
            Console.WriteLine("Se inicializo la page");
        }

        private async void WaitingRoomPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is WaitingRoomViewModel viewModel)
            {
                await viewModel.LoadInitialData();
            }
        }
    }
}
