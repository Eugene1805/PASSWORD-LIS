using PASSWORD_LIS_Client.ViewModels;
using System;
using System.Windows;

namespace PASSWORD_LIS_Client.Views
{
    public partial class YesNoPopUpWindow : Window
    {
        public YesNoPopUpWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs events)
        {
            if (events.OldValue is YesNoPopUpViewModel oldViewModel)
            {
                oldViewModel.CloseRequested -= HandleViewModelCloseRequested;
            }
            if (events.NewValue is YesNoPopUpViewModel newViewModel)
            {
                newViewModel.CloseRequested += HandleViewModelCloseRequested;
            }
        }

        private void HandleViewModelCloseRequested(bool? dialogResult)
        {
            this.DialogResult = dialogResult;
            this.Close();
        }

        protected override void OnClosed(EventArgs events)
        {
            if (DataContext is YesNoPopUpViewModel currentViewModel)
            {
                currentViewModel.CloseRequested -= HandleViewModelCloseRequested;
            }
            base.OnClosed(events);
        }
    }
}
