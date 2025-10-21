using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.FriendsManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

        private FriendDTO selectedRequest;
        public FriendDTO SelectedRequest 
        { 
            get => selectedRequest; 
            set { selectedRequest = value; OnPropertyChanged(); } 
        }

        public ICommand AcceptRequestCommand { get; }
        public ICommand RejectRequestCommand { get; }

        private readonly IFriendsManagerService friendsService;

        public FriendRequestsViewModel(IFriendsManagerService friendsService)
        {
            this.friendsService = friendsService;
            PendingRequests = new ObservableCollection<FriendDTO>();
            AcceptRequestCommand = new RelayCommand(async (_) => await RespondToRequest(true), (_) => SelectedRequest != null);
            RejectRequestCommand = new RelayCommand(async (_) => await RespondToRequest(false), (_) => SelectedRequest != null);

            _ = LoadPendingRequestsAsync();
        }

        private async Task LoadPendingRequestsAsync()
        {
            var requests = await friendsService.GetPendingRequestsAsync();
            PendingRequests = new ObservableCollection<FriendDTO>(requests);
        }

        private async Task RespondToRequest(bool accepted)
        {
            var requestToRespond = SelectedRequest;
            if (requestToRespond == null) return;

            await friendsService.RespondToFriendRequestAsync(requestToRespond.PlayerId, accepted);

            // Quitar de la lista local para feedback instantáneo
            PendingRequests.Remove(requestToRespond);
        }
    }
}
