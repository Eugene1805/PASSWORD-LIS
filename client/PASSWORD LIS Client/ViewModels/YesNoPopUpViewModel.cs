using PASSWORD_LIS_Client.Commands;
using System;
using System.Windows.Input;

namespace PASSWORD_LIS_Client.ViewModels
{
    internal class YesNoPopUpViewModel : BaseViewModel
    {
        public string Title { get; }
        public string Message { get; }

        public ICommand YesCommand { get; }
        public ICommand NoCommand { get; }

        public event Action<bool?> CloseRequested;

        public YesNoPopUpViewModel(string title, string message)
        {
            Title = title; 
            Message = message;

            YesCommand = new RelayCommand(ExecuteYes);
            NoCommand = new RelayCommand(ExecuteNo);

        }

        private void ExecuteYes (object parameter)
        {
            CloseRequested?.Invoke(true);
        }

        private void ExecuteNo(object parameter)
        {
            CloseRequested?.Invoke(false);
        }
    }
}
