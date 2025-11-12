using PASSWORD_LIS_Client.AccountManagerServiceReference;
using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.FriendsManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class FriendRequestsViewModel : BaseViewModel
    {
        private ObservableCollection<FriendDTO> pendingRequests;
        public ObservableCollection<FriendDTO> PendingRequests 
        { 
            get => pendingRequests; 
            set { pendingRequests = value; OnPropertyChanged(); } 
        }

        private bool isLoading;
        public bool IsLoading
        {
            get => isLoading;
            set 
            {  
                isLoading = value; 
                OnPropertyChanged(); 
                UpdateMessageVisibility();
                RelayCommand.RaiseCanExecuteChanged();
            }
        }

        private bool showNoRequestsMessage;
        public bool ShowNoRequestsMessage
        {
            get => showNoRequestsMessage;
            set { showNoRequestsMessage = value; OnPropertyChanged(); }
        }

        private FriendDTO selectedRequest;
        public FriendDTO SelectedRequest 
        { 
            get => selectedRequest; 
            set 
            { 
                selectedRequest = value; 
                OnPropertyChanged();
                RelayCommand.RaiseCanExecuteChanged();
            } 
        }

        public ICommand AcceptRequestCommand { get; }
        public ICommand RejectRequestCommand { get; }

        private readonly IFriendsManagerService friendsService;
        private readonly IWindowService windowService;

        public FriendRequestsViewModel(IFriendsManagerService friendsService, IWindowService windowService)
        {
            this.friendsService = friendsService;
            this.windowService = windowService;
            PendingRequests = new ObservableCollection<FriendDTO>();
            AcceptRequestCommand = new RelayCommand(async (_) => await RespondToRequest(true), (_) => SelectedRequest != null &&!IsLoading);
            RejectRequestCommand = new RelayCommand(async (_) => await RespondToRequest(false), (_) => SelectedRequest != null && !IsLoading);

            _ = LoadPendingRequestsAsync();
        }

        private async Task LoadPendingRequestsAsync()
        {
            IsLoading = true;
            try
            {
                var requests = await friendsService.GetPendingRequestsAsync();
                PendingRequests = new ObservableCollection<FriendDTO>(requests);

            }
            catch (FaultException<ServiceErrorDetailDTO> ex)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText, 
                    ex.Detail.Message, PopUpIcon.Error);
            }
            catch (TimeoutException)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.timeLimitTitleText, 
                    Properties.Langs.Lang.serverTimeoutText, PopUpIcon.Warning);
            }
            catch (EndpointNotFoundException)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.connectionErrorTitleText, 
                    Properties.Langs.Lang.serverConnectionInternetErrorText, PopUpIcon.Error);
            }
            catch (CommunicationException)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.networkErrorTitleText, 
                    Properties.Langs.Lang.serverCommunicationErrorText, PopUpIcon.Error);
            }
            catch (Exception)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error); 
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RespondToRequest(bool accepted)
        {
            var requestToRespond = SelectedRequest;
            if (requestToRespond == null)
            {
                return;
            }
            IsLoading = true; 
            try
            {
                await friendsService.RespondToFriendRequestAsync(requestToRespond.PlayerId, accepted);

                PendingRequests.Remove(requestToRespond);
                SelectedRequest = null; 


            }
            catch (FaultException<ServiceErrorDetailDTO> ex)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText, 
                    ex.Detail.Message, PopUpIcon.Error);
            }
            catch (TimeoutException)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.timeLimitTitleText, 
                    Properties.Langs.Lang.serverTimeoutText, PopUpIcon.Warning);
            }
            catch (EndpointNotFoundException)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.connectionErrorTitleText, 
                    Properties.Langs.Lang.serverConnectionInternetErrorText, PopUpIcon.Error);
            }
            catch (CommunicationException)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.networkErrorTitleText, 
                    Properties.Langs.Lang.serverCommunicationErrorText, PopUpIcon.Error);
            }
            catch (Exception)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateMessageVisibility()
        {
            ShowNoRequestsMessage = !IsLoading && !PendingRequests.Any();
        }
    }
}
