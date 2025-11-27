using PASSWORD_LIS_Client.AccountManagerServiceReference;
using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.FriendsManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class AddFriendViewModel : BaseViewModel
    {
        private string email;
        public string Email
        {
            get => email;
            set { email = value; ValidateEmail(); OnPropertyChanged(); }
        }

        private string emailError;
        public string EmailError
        {
            get => emailError;
            set { emailError = value; OnPropertyChanged(); }
        }

        private bool isSending;
        public bool IsSending
        {
            get => isSending;
            set { isSending = value; OnPropertyChanged(); }
        }

        public ICommand SendRequestCommand { get; }

        private readonly IFriendsManagerService friendsService;

        public AddFriendViewModel(IFriendsManagerService friendsService, IWindowService windowService) 
            : base(windowService)
        {
            this.friendsService = friendsService;
            SendRequestCommand = new RelayCommand(async (_) => await SendRequestAsync(), (_) => !IsSending && !string.IsNullOrWhiteSpace(Email));
        }

        private async Task SendRequestAsync()
        {
            if (!string.IsNullOrWhiteSpace(EmailError) || string.IsNullOrWhiteSpace(Email))
            {
                return;
            }

            IsSending = true;
            try
            {
                await ExecuteAsync(async () =>
                {
                    var result = await friendsService.SendFriendRequestAsync(Email);
                    HandleFriendRequestResult(result);
                });
            }
            finally
            {
                IsSending = false;
            }
        }

        private void HandleFriendRequestResult(FriendRequestResult result)
        {
            switch (result)
            {
                case FriendRequestResult.Success:
                    windowService.CloseWindow(this);
                    windowService.ShowPopUp(Properties.Langs.Lang.requestSentTitleText,
                        Properties.Langs.Lang.requestSentText, PopUpIcon.Success);
                    break;
                case FriendRequestResult.UserNotFound:
                    windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                        Properties.Langs.Lang.playerNotFoundText, PopUpIcon.Warning);
                    break;
                case FriendRequestResult.AlreadyFriends:
                    windowService.ShowPopUp(Properties.Langs.Lang.informationText,
                        Properties.Langs.Lang.existingFriendshipText, PopUpIcon.Information);
                    break;
                case FriendRequestResult.RequestAlreadySent:
                    windowService.ShowPopUp(Properties.Langs.Lang.informationText, 
                        Properties.Langs.Lang.existingFriendRequestText, PopUpIcon.Information);
                    break;
                case FriendRequestResult.CannotAddSelf:
                    windowService.ShowPopUp(Properties.Langs.Lang.informationText,
                        Properties.Langs.Lang.friendRequestToYourselfText, PopUpIcon.Information);
                    break;
                case FriendRequestResult.RequestAlreadyReceived:
                    windowService.ShowPopUp(Properties.Langs.Lang.informationText,
                        Properties.Langs.Lang.friendRequestInboxText, PopUpIcon.Information);
                    break;
                default:
                    windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                        Properties.Langs.Lang.couldNotSentRequestText, PopUpIcon.Error);
                    break;
            }
        }
    }
}
