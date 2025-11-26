using PASSWORD_LIS_Client.AccountManagerServiceReference;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class BaseViewModel : INotifyPropertyChanged
    {
        protected IWindowService windowService;

        public BaseViewModel() { }

        public BaseViewModel(IWindowService windowService)
        {
            this.windowService = windowService;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
            {
                return false;
            }

            backingStore = value;
            OnPropertyChanged(propertyName);

            return true;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected async Task<T> ExecuteAsync<T>(Func<Task<T>> func)
        {
            try
            {
                return await func();
            }
            catch (FaultException<ServiceErrorDetailDTO> ex)
            {
                HandleServiceError(ex.Detail);
                throw;
            }
            catch (TimeoutException)
            {
                windowService?.ShowPopUp(Properties.Langs.Lang.timeLimitTitleText,
                    Properties.Langs.Lang.serverTimeoutText, PopUpIcon.Warning);
                throw;
            }
            catch (EndpointNotFoundException)
            {
                windowService?.ShowPopUp(Properties.Langs.Lang.connectionErrorTitleText,
                    Properties.Langs.Lang.serverConnectionInternetErrorText, PopUpIcon.Error);
                throw;
            }
            catch (CommunicationException)
            {
                windowService?.ShowPopUp(Properties.Langs.Lang.networkErrorTitleText,
                    Properties.Langs.Lang.serverCommunicationErrorText, PopUpIcon.Error);
                throw;
            }
            catch (Exception)
            {
                windowService?.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
                throw;
            }
        }

        protected async Task ExecuteAsync(Func<Task> func)
        {
            await ExecuteAsync(async () => { await func(); return true; });
        }

        protected T Execute<T>(Func<T> func)
        {
            try
            {
                return func();
            }
            catch (FaultException<ServiceErrorDetailDTO> ex)
            {
                HandleServiceError(ex.Detail);
                throw;
            }
            catch (TimeoutException)
            {
                windowService?.ShowPopUp(Properties.Langs.Lang.timeLimitTitleText,
                    Properties.Langs.Lang.serverTimeoutText, PopUpIcon.Warning);
                throw;
            }
            catch (EndpointNotFoundException)
            {
                windowService?.ShowPopUp(Properties.Langs.Lang.connectionErrorTitleText,
                    Properties.Langs.Lang.serverConnectionInternetErrorText, PopUpIcon.Error);
                throw;
            }
            catch (CommunicationException)
            {
                windowService?.ShowPopUp(Properties.Langs.Lang.networkErrorTitleText,
                    Properties.Langs.Lang.serverCommunicationErrorText, PopUpIcon.Error);
                throw;
            }
            catch (Exception)
            {
                windowService?.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
                throw;
            }
        }

        private void HandleServiceError(ServiceErrorDetailDTO errorDetail)
        {
            if (windowService == null || errorDetail == null)
            {
                return;
            }

            string title;
            string message;
            PopUpIcon icon = PopUpIcon.Error;

            switch (errorDetail.ErrorCode)
            {
                case "USER_ALREADY_EXISTS":
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.userAlreadyExistText;
                    icon = PopUpIcon.Warning;
                    break;
                case "DATABASE_ERROR":
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.unexpectedErrorText;
                    break;
                case "EMAIL_SENDING_ERROR":
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.unexpectedErrorText;
                    break;
                case "EMAIL_CONFIGURATION_ERROR":
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.unexpectedErrorText;
                    break;
                case "NULL_ARGUMENT":
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.unexpectedErrorText;
                    break;
                case "INVALID_OPERATION":
                    title = Properties.Langs.Lang.errorTitleText;
                    message = errorDetail.Message;
                    icon = PopUpIcon.Warning;
                    break;
                case "ROOM_NOT_FOUND":
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.unexpectedErrorText;
                    icon = PopUpIcon.Warning;
                    break;
                case "ROOM_FULL":
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.unexpectedErrorText;
                    icon = PopUpIcon.Warning;
                    break;
                case "PLAYER_NOT_FOUND":
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.unexpectedErrorText;
                    icon = PopUpIcon.Warning;
                    break;
                case "ALREADY_IN_ROOM":
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.unexpectedErrorText;
                    icon = PopUpIcon.Warning;
                    break;
                case "COULD_NOT_CREATE_ROOM":
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.unexpectedErrorText;
                    icon = PopUpIcon.Warning;
                    break;
                default:
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.unexpectedErrorText;
                    break;
            }

            windowService.ShowPopUp(title, message, icon);
        }
    }
}
