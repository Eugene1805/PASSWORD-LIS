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
        private readonly IFriendsManagerService friendsService;
        private const int MaximumEmailLength = 100;

        private string email;
        public string Email
        {
            get => email;
            set 
            { 
                email = value; 
                ValidateEmail(); 
                OnPropertyChanged(); 
            }
        }

        private string emailError;
        public string EmailError
        {
            get => emailError;
            set 
            { 
                emailError = value; 
                OnPropertyChanged(); 
            }
        }

        private bool isSending;
        public bool IsSending
        {
            get => isSending;
            set 
            { 
                isSending = value; 
                OnPropertyChanged(); 
            }
        }

        public ICommand SendRequestCommand 
        { 
            get; 
        }

        public AddFriendViewModel(IFriendsManagerService FriendsService, IWindowService WindowService) 
            : base(WindowService)
        {
            this.friendsService = FriendsService;
            SendRequestCommand = new RelayCommand(async (_) => await SendRequestAsync(), (_) => !IsSending);
        }

        private async Task SendRequestAsync()
        {
            if (!ValidateEmail())
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

        private bool ValidateEmail()
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                EmailError = Properties.Langs.Lang.emptyEmailText; 
                return false;
            }

            if (Email.Length > MaximumEmailLength)
            {
                EmailError = Properties.Langs.Lang.emailTooLongText;
                return false;
            }

            if (!ValidationUtils.IsValidEmail(Email))
            {
                EmailError = Properties.Langs.Lang.invalidEmailFormatText;
                return false;
            }

            EmailError = null;
            return true;
        }
    }
}
