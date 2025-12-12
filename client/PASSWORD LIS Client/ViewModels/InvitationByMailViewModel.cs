using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class InvitationByMailViewModel : BaseViewModel
    {
        private readonly IWaitingRoomManagerService roomManagerClient;
        private readonly string gameCode;
        private readonly string inviterNickname;
        private const int MaximumEmailLength = 100;

        private string email;
        public string Email
        {
            get => email;
            set
            {
                ValidateEmail();
                SetProperty(ref email, value);
            }
        }

        private string emailError;
        public string EmailError
        {
            get => emailError;
            set => SetProperty(ref emailError, value);
        }

        private bool isSending;
        public bool IsSending
        {
            get => isSending;
            set => SetProperty(ref isSending, value);
        }

        public ICommand SendInvitationCommand 
        { 
            get; 
        }

        public InvitationByMailViewModel(IWaitingRoomManagerService RoomManagerClient,
            IWindowService WindowService, string GameCode, string InviterNickname) : base(WindowService)
        {
            this.roomManagerClient = RoomManagerClient;
            this.windowService = WindowService;
            this.gameCode = GameCode;
            this.inviterNickname = InviterNickname;

            SendInvitationCommand = new RelayCommand(async (_) => await SendInvitationAsync(), (_) => !IsSending);
        }

        private async Task SendInvitationAsync()
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
                    await roomManagerClient.SendGameInvitationByEmailAsync(Email, gameCode, inviterNickname);

                    windowService.ShowPopUp(Properties.Langs.Lang.successTitleText,
                        Properties.Langs.Lang.invitationsentSuccessText, PopUpIcon.Success);

                    windowService.CloseWindow(this);
                });
            }
            finally
            {
                IsSending = false;
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
