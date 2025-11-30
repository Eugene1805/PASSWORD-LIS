using log4net;
using PASSWORD_LIS_Client.AccountManagerServiceReference;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class BaseViewModel : INotifyPropertyChanged
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseViewModel));
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
                HandleTypedFaultException(ex);
                return default;
            }
            catch (FaultException ex)
            {
                HandleUntypedFaultException(ex);
                return default;
            }
            catch (TimeoutException ex)
            {
                HandleTimeoutException(ex);
                return default;
            }
            catch (EndpointNotFoundException ex)
            {
                HandleEndpointNotFoundException(ex);
                return default;
            }
            catch (CommunicationException ex)
            {
                HandleCommunicationException(ex);
                return default;
            }
            catch (Exception ex)
            {
                HandleGenericException(ex);
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
                HandleTypedFaultException(ex);
            }
            catch (FaultException ex)
            {
                HandleUntypedFaultException(ex);
            }
            catch (TimeoutException ex)
            {
                HandleTimeoutException(ex);
            }
            catch (EndpointNotFoundException ex)
            {
                HandleEndpointNotFoundException(ex);
            }
            catch (CommunicationException ex)
            {
                HandleCommunicationException(ex);
            }
            catch (Exception ex)
            {
                HandleGenericException(ex);
            }
        }

        private void HandleTypedFaultException(FaultException<ServiceErrorDetailDTO> ex)
        {
            var errorCode = ex.Detail?.ErrorCode ?? "UNKNOWN";
            log.WarnFormat("Service fault exception caught. ErrorCode: {0}, Message: {1}", 
                errorCode, ex.Message);
            HandleServiceError(ex.Detail);
        }

        private void HandleUntypedFaultException(FaultException ex)
        {
            if (FaultHelpers.TryConvertToTypedFault<ServiceErrorDetailDTO>(ex, out var typedFault))
            {
                var clientDto = CreateClientDto(typedFault.Detail);
                log.InfoFormat("Successfully converted untyped fault to typed. ErrorCode: {0}", 
                    clientDto.ErrorCode ?? "NONE");
                HandleServiceError(clientDto);
                return;
            }

            var errorCodeFromXml = FaultHelpers.GetErrorCodeFromFault(ex);
            var messageFromXml = ExtractMessageFromFault(ex);

            if (!string.IsNullOrEmpty(errorCodeFromXml) || !string.IsNullOrEmpty(messageFromXml))
            {
                log.InfoFormat("Extracted fault details from XML. ErrorCode: {0}, Message: {1}", 
                    errorCodeFromXml ?? "NONE", messageFromXml ?? "NONE");
                HandleServiceError(errorCodeFromXml, messageFromXml);
                return;
            }

            log.Warn("Could not extract fault details from exception, using default error handling");
            HandleServiceError((ServiceErrorDetailDTO)null);
        }

        private ServiceErrorDetailDTO CreateClientDto(ServiceErrorDetailDTO serverDto)
        {
            return new ServiceErrorDetailDTO
            {
                Code = serverDto.Code,
                ErrorCode = serverDto?.ErrorCode,
                Message = serverDto?.Message
            };
        }

        private static string ExtractMessageFromFault(FaultException ex)
        {
            try
            {
                var messageFault = ex.CreateMessageFault();
                if (!messageFault.HasDetail)
                {
                    return null;
                }

                var reader = messageFault.GetReaderAtDetailContents();
                var xml = reader.ReadOuterXml();
                var xElement = XElement.Parse(xml);

                return xElement.Descendants()
                    .FirstOrDefault(node =>
                        string.Equals(node.Name.LocalName, "Message", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(node.Name.LocalName, "message", StringComparison.OrdinalIgnoreCase))
                    ?.Value;
            }
            catch (Exception extractEx)
            {
                log.DebugFormat("Failed to extract message from fault XML: {0}", extractEx.Message);
                return null;
            }
        }

        private void HandleTimeoutException(TimeoutException ex)
        {
            log.WarnFormat("Timeout exception caught. Message: {0}", ex.Message);
            windowService?.ShowPopUp(
                Properties.Langs.Lang.timeLimitTitleText,
                Properties.Langs.Lang.serverTimeoutText,
                PopUpIcon.Warning);
        }

        private void HandleEndpointNotFoundException(EndpointNotFoundException ex)
        {
            log.ErrorFormat("Endpoint not found exception caught. Message: {0}", ex.Message);
            windowService?.ShowPopUp(
                Properties.Langs.Lang.connectionErrorTitleText,
                Properties.Langs.Lang.serverConnectionInternetErrorText,
                PopUpIcon.Error);
        }

        private void HandleCommunicationException(CommunicationException ex)
        {
            log.ErrorFormat("Communication exception caught. Type: {0}, Message: {1}", 
                ex.GetType().Name, ex.Message);
            windowService?.ShowPopUp(
                Properties.Langs.Lang.networkErrorTitleText,
                Properties.Langs.Lang.serverCommunicationErrorText,
                PopUpIcon.Error);
        }

        private void HandleGenericException(Exception ex)
        {
            log.ErrorFormat("Unexpected exception caught. Type: {0}, Message: {1}, StackTrace: {2}", 
                ex.GetType().Name, ex.Message, ex.StackTrace);
            windowService?.ShowPopUp(
                Properties.Langs.Lang.errorTitleText,
                Properties.Langs.Lang.unexpectedErrorText,
                PopUpIcon.Error);
        }

        private void HandleServiceError(ServiceErrorDetailDTO errorDetail)
        {
            if (windowService == null)
            {
                log.Warn("WindowService is null, cannot display error popup");
                return;
            }

            if (errorDetail == null)
            {
                log.Warn("ErrorDetail is null, displaying default error message");
                windowService.ShowPopUp(
                    Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.unexpectedErrorText,
                    PopUpIcon.Error);
                return;
            }

            var errorInfo = GetErrorInfo(errorDetail.Code);
            log.InfoFormat("Displaying service error to user. ErrorCode: {0}, Title: {1}", 
                errorDetail.Code, errorInfo.Title);

            if (!string.IsNullOrEmpty(errorInfo.Title) || !string.IsNullOrEmpty(errorInfo.Message))
            {
                windowService.ShowPopUp(errorInfo.Title, errorInfo.Message, errorInfo.Icon);
            }
        }

        private void HandleServiceError(string errorCode, string message)
        {
            log.InfoFormat("Handling service error with errorCode: {0}, message: {1}", 
                errorCode ?? "NONE", message ?? "NONE");
            var dto = new ServiceErrorDetailDTO
            {
                ErrorCode = errorCode,
                Message = message
            };
            HandleServiceError(dto);
        }

        private static (string Title, string Message, PopUpIcon Icon) GetErrorInfo(ServiceErrorCode errorCode)
        {
            switch (errorCode)
            {
                case ServiceErrorCode.USER_ALREADY_EXISTS:
                    return (
                        Properties.Langs.Lang.errorTitleText,
                        Properties.Langs.Lang.userAlreadyExistText,
                        PopUpIcon.Warning
                    );

                case ServiceErrorCode.DATABASE_ERROR:
                    return (
                        Properties.Langs.Lang.errorTitleText,
                        Properties.Langs.Lang.serverCommunicationErrorText,
                        PopUpIcon.Error
                    );

                case ServiceErrorCode.EMAIL_SENDING_ERROR:
                case ServiceErrorCode.EMAIL_CONFIGURATION_ERROR:
                    return (
                        Properties.Langs.Lang.warningTitleText,
                        Properties.Langs.Lang.couldNotSendMail,
                        PopUpIcon.Warning
                    );

                case ServiceErrorCode.NULL_ARGUMENT:
                case ServiceErrorCode.INVALID_OPERATION:
                    return (
                        Properties.Langs.Lang.warningTitleText,
                        Properties.Langs.Lang.invalidArgument,
                        PopUpIcon.Error
                    );

                case ServiceErrorCode.ROOM_NOT_FOUND:
                    return (
                        Properties.Langs.Lang.errorTitleText,
                        Properties.Langs.Lang.roomNotFoundText,
                        PopUpIcon.Warning
                    );

                case ServiceErrorCode.ROOM_FULL:
                    return (
                        Properties.Langs.Lang.warningTitleText,
                        Properties.Langs.Lang.roomFullText,
                        PopUpIcon.Warning
                    );

                case ServiceErrorCode.PLAYER_NOT_FOUND:
                    return (
                        Properties.Langs.Lang.warningTitleText,
                        Properties.Langs.Lang.playerNotFoundText,
                        PopUpIcon.Warning
                    );

                case ServiceErrorCode.ALREADY_IN_ROOM:
                    return (
                        Properties.Langs.Lang.errorTitleText,
                        Properties.Langs.Lang.youAreAlreadyInGameText,
                        PopUpIcon.Warning
                    );

                case ServiceErrorCode.COULD_NOT_CREATE_ROOM:
                    return (
                        Properties.Langs.Lang.errorTitleText,
                        Properties.Langs.Lang.errorInitializingGameText,
                        PopUpIcon.Error
                    );

                case ServiceErrorCode.STATISTICS_ERROR:
                    return (
                        Properties.Langs.Lang.errorTitleText,
                        Properties.Langs.Lang.statisticsErrorText,
                        PopUpIcon.Error
                    );

                case ServiceErrorCode.INVALID_REPORT_PAYLOAD:
                    return (
                        Properties.Langs.Lang.errorTitleText,
                        Properties.Langs.Lang.invalidReportDataText,
                        PopUpIcon.Error
                    );

                case ServiceErrorCode.REPORTER_NOT_FOUND:
                    return (
                        Properties.Langs.Lang.warningTitleText,
                        Properties.Langs.Lang.acceptText,
                        PopUpIcon.Warning
                    );

                case ServiceErrorCode.REPORTED_PLAYER_NOT_FOUND:
                    return (
                        Properties.Langs.Lang.warningTitleText,
                        Properties.Langs.Lang.reportedPlayerNotFoundText,
                        PopUpIcon.Warning
                    );

                case ServiceErrorCode.PLAYER_ALREADY_BANNED:
                    return (
                        Properties.Langs.Lang.warningTitleText,
                        Properties.Langs.Lang.playerAlreadyBannedText,
                        PopUpIcon.Warning
                    );

                case ServiceErrorCode.BAN_PERSISTENCE_ERROR:
                    return (
                        Properties.Langs.Lang.errorTitleText,
                        Properties.Langs.Lang.banPersistenceErrorText,
                        PopUpIcon.Error
                    );

                case ServiceErrorCode.SUBSCRIPTION_ERROR:
                    return (
                        Properties.Langs.Lang.errorTitleText,
                        Properties.Langs.Lang.subscriptionErrorText,
                        PopUpIcon.Error
                    );

                case ServiceErrorCode.UNSUBSCRIPTION_ERROR:
                    return (
                        Properties.Langs.Lang.errorTitleText,
                        Properties.Langs.Lang.unsubscriptionErrorText,
                        PopUpIcon.Error
                    );

                case ServiceErrorCode.SECURITY_ERROR:
                    return (
                        Properties.Langs.Lang.errorTitleText,
                        Properties.Langs.Lang.securityErrorText,
                        PopUpIcon.Error
                    );

                case ServiceErrorCode.FORMAT_ERROR:
                    return (
                        Properties.Langs.Lang.warningTitleText,
                        Properties.Langs.Lang.formatErrorText,
                        PopUpIcon.Warning
                    );

                case ServiceErrorCode.SELF_INVITATION:
                    return (
                        Properties.Langs.Lang.warningTitleText,
                        Properties.Langs.Lang.selfInvitationText,
                        PopUpIcon.Warning
                    );

                case ServiceErrorCode.MATCH_NOT_FOUND_OR_ENDED:
                    return (
                        Properties.Langs.Lang.warningTitleText,
                        Properties.Langs.Lang.matchNotFoundOrEndedText,
                        PopUpIcon.Warning
                    );

                case ServiceErrorCode.MATCH_ALREADY_STARTED_OR_FINISHING:
                    return (
                        Properties.Langs.Lang.warningTitleText,
                        Properties.Langs.Lang.matchStartedOrFinishingText,
                        PopUpIcon.Warning
                    );

                case ServiceErrorCode.NOT_AUTHORIZED_TO_JOIN:
                    return (
                        Properties.Langs.Lang.errorTitleText,
                        Properties.Langs.Lang.acceptText,
                        PopUpIcon.Warning
                    );

                case ServiceErrorCode.MAX_ONE_REPORT_PER_BAN:
                    return (
                        Properties.Langs.Lang.warningTitleText,
                        Properties.Langs.Lang.maxReportLimitText,
                        PopUpIcon.Warning
                    );

                case ServiceErrorCode.COULD_NOT_CREATE_GAME:
                    return (
                        Properties.Langs.Lang.errorTitleText,
                        Properties.Langs.Lang.couldNotCreateMatch,
                        PopUpIcon.Error
                    );

                default:
                    return (
                        Properties.Langs.Lang.errorTitleText,
                        Properties.Langs.Lang.unexpectedErrorText,
                        PopUpIcon.Error
                    );
            }
        }
    }
}
