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
                return default;
            }
            catch (TimeoutException)
            {
                windowService?.ShowPopUp(Properties.Langs.Lang.timeLimitTitleText,
                    Properties.Langs.Lang.serverTimeoutText, PopUpIcon.Warning);
                return default;
            }
            catch (EndpointNotFoundException)
            {
                windowService?.ShowPopUp(Properties.Langs.Lang.connectionErrorTitleText,
                    Properties.Langs.Lang.serverConnectionInternetErrorText, PopUpIcon.Error);
                return default;
            }
            catch (CommunicationException)
            {
                windowService?.ShowPopUp(Properties.Langs.Lang.networkErrorTitleText,
                    Properties.Langs.Lang.serverCommunicationErrorText, PopUpIcon.Error);
                return default;
            }
            catch (Exception)
            {
                windowService?.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
                return default;
            }
        }

        protected async Task ExecuteAsync(Func<Task> func)
        {
            await ExecuteAsync(async () => { await func(); return true; });
        }

        protected void Execute(Action action)
        {
            try
            {
                action();
            }
            catch (FaultException<ServiceErrorDetailDTO> ex)
            {
                HandleServiceError(ex.Detail);
            }
            catch (TimeoutException)
            {
                windowService?.ShowPopUp(Properties.Langs.Lang.timeLimitTitleText,
                    Properties.Langs.Lang.serverTimeoutText, PopUpIcon.Warning);
            }
            catch (EndpointNotFoundException)
            {
                windowService?.ShowPopUp(Properties.Langs.Lang.connectionErrorTitleText,
                    Properties.Langs.Lang.serverConnectionInternetErrorText, PopUpIcon.Error);
            }
            catch (CommunicationException)
            {
                windowService?.ShowPopUp(Properties.Langs.Lang.networkErrorTitleText,
                    Properties.Langs.Lang.serverCommunicationErrorText, PopUpIcon.Error);
            }
            catch (Exception)
            {
                windowService?.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
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
            PopUpIcon icon;

            switch (errorDetail.Code)
            {
                case ServiceErrorCode.USER_ALREADY_EXISTS:
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.userAlreadyExistText;
                    icon = PopUpIcon.Warning;
                    break;

                case ServiceErrorCode.DATABASE_ERROR:
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.serverCommunicationErrorText;
                    icon = PopUpIcon.Error;
                    break;

                case ServiceErrorCode.EMAIL_SENDING_ERROR:
                case ServiceErrorCode.EMAIL_CONFIGURATION_ERROR:
                    title = Properties.Langs.Lang.warningTitleText;
                    message = Properties.Langs.Lang.couldNotSendMail;
                    icon = PopUpIcon.Warning;
                    break;

                case ServiceErrorCode.NULL_ARGUMENT:

                case ServiceErrorCode.INVALID_OPERATION:
                    title = Properties.Langs.Lang.warningTitleText;
                    message = Properties.Langs.Lang.invalidArgument;
                    icon = PopUpIcon.Error;
                    break;

                case ServiceErrorCode.ROOM_NOT_FOUND:
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.roomNotFoundText;
                    icon = PopUpIcon.Warning;
                    break;

                case ServiceErrorCode.ROOM_FULL:
                    title = Properties.Langs.Lang.warningTitleText;
                    message = Properties.Langs.Lang.roomFullText;
                    icon = PopUpIcon.Warning;
                    break;

                case ServiceErrorCode.PLAYER_NOT_FOUND:
                    title = Properties.Langs.Lang.warningTitleText;
                    message = Properties.Langs.Lang.playerNotFoundText;
                    icon = PopUpIcon.Warning;
                    break;

                case ServiceErrorCode.ALREADY_IN_ROOM:
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.youAreAlreadyInGameText;
                    icon = PopUpIcon.Warning;
                    break;

                case ServiceErrorCode.COULD_NOT_CREATE_ROOM:
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.errorInitializingGameText;
                    icon = PopUpIcon.Error;
                    break;

                case ServiceErrorCode.STATISTICS_ERROR:
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.statisticsErrorText;
                    icon = PopUpIcon.Error;
                    break;

                case ServiceErrorCode.INVALID_REPORT_PAYLOAD:
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.invalidReportDataText;
                    icon = PopUpIcon.Error;
                    break;

                case ServiceErrorCode.REPORTER_NOT_FOUND:
                    title = Properties.Langs.Lang.warningTitleText;
                    message = Properties.Langs.Lang.acceptText;
                    icon = PopUpIcon.Warning;
                    break;

                case ServiceErrorCode.REPORTED_PLAYER_NOT_FOUND:
                    title = Properties.Langs.Lang.warningTitleText;
                    message = Properties.Langs.Lang.reportedPlayerNotFoundText;
                    icon = PopUpIcon.Warning;
                    break;

                case ServiceErrorCode.PLAYER_ALREADY_BANNED:
                    title = Properties.Langs.Lang.warningTitleText;
                    message = Properties.Langs.Lang.playerAlreadyBannedText;
                    icon = PopUpIcon.Warning;
                    break;

                case ServiceErrorCode.BAN_PERSISTENCE_ERROR:
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.banPersistenceErrorText;
                    icon = PopUpIcon.Error;
                    break;

                case ServiceErrorCode.SUBSCRIPTION_ERROR:
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.subscriptionErrorText;
                    icon = PopUpIcon.Error;
                    break;

                case ServiceErrorCode.UNSUBSCRIPTION_ERROR:
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.unsubscriptionErrorText;
                    icon = PopUpIcon.Error;
                    break;

                case ServiceErrorCode.SECURITY_ERROR:
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.securityErrorText;
                    icon = PopUpIcon.Error;
                    break;

                case ServiceErrorCode.FORMAT_ERROR:
                    title = Properties.Langs.Lang.warningTitleText;
                    message = Properties.Langs.Lang.formatErrorText;
                    icon = PopUpIcon.Warning;
                    break;

                case ServiceErrorCode.SELF_INVITATION:
                    title = Properties.Langs.Lang.warningTitleText;
                    message = Properties.Langs.Lang.selfInvitationText;
                    icon = PopUpIcon.Warning;
                    break;

                case ServiceErrorCode.MATCH_NOT_FOUND_OR_ENDED:
                    title = Properties.Langs.Lang.warningTitleText;
                    message = Properties.Langs.Lang.matchNotFoundOrEndedText;
                    icon = PopUpIcon.Warning;
                    break;

                case ServiceErrorCode.MATCH_ALREADY_STARTED_OR_FINISHING:
                    title = Properties.Langs.Lang.warningTitleText;
                    message = Properties.Langs.Lang.matchStartedOrFinishingText;
                    icon = PopUpIcon.Warning;
                    break;

                case ServiceErrorCode.NOT_AUTHORIZED_TO_JOIN:
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.acceptText;
                    icon = PopUpIcon.Warning;
                    break;

                case ServiceErrorCode.MAX_ONE_REPORT_PER_BAN:
                    title = Properties.Langs.Lang.warningTitleText;
                    message = Properties.Langs.Lang.maxReportLimitText;
                    icon = PopUpIcon.Warning;
                    break;

                case ServiceErrorCode.COULD_NOT_CREATE_GAME:
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.couldNotCreateMatch;
                    icon = PopUpIcon.Error;
                    break;

                default:
                    title = Properties.Langs.Lang.errorTitleText;
                    message = Properties.Langs.Lang.unexpectedErrorText;
                    icon = PopUpIcon.Error;
                    break;
            }

            if (!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(message))
            {
                windowService.ShowPopUp(title, message, icon);
            }
        }
    }
}
