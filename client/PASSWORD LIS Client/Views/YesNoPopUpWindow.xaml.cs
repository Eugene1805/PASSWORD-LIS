using PASSWORD_LIS_Client.ViewModels;
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

namespace PASSWORD_LIS_Client.Views
{
    /// <summary>
    /// Lógica de interacción para YesNoPopUpWindow.xaml
    /// </summary>
    public partial class YesNoPopUpWindow : Window
    {
        public YesNoPopUpWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is YesNoPopUpViewModel oldViewModel)
            {
                oldViewModel.CloseRequested -= HandleViewModelCloseRequested;
            }
            if (e.NewValue is YesNoPopUpViewModel newViewModel)
            {
                newViewModel.CloseRequested += HandleViewModelCloseRequested;
            }
        }

        private void HandleViewModelCloseRequested(bool? dialogResult)
        {
            // Establece el DialogResult de la ventana
            // Esto es lo que ShowDialog() devolverá
            this.DialogResult = dialogResult;
            this.Close(); // Cierra la ventana
        }

        protected override void OnClosed(EventArgs e)
        {
            if (DataContext is YesNoPopUpViewModel currentViewModel)
            {
                currentViewModel.CloseRequested -= HandleViewModelCloseRequested;
            }
            base.OnClosed(e);
        }
    }
}
