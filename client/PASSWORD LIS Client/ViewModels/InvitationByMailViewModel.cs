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
        private string email;
        public string Email
        {
            get => email;
            set => SetProperty(ref email, value);
        }

        private bool isSending;
        public bool IsSending
        {
            get => isSending;
            set => SetProperty(ref isSending, value);
        }

        public ICommand SendInvitationCommand { get; }

        private readonly IWaitingRoomManagerService roomManagerClient;
        private readonly string gameCode;
        private readonly string inviterNickname;

        public InvitationByMailViewModel (IWaitingRoomManagerService roomManagerClient,
            IWindowService windowService, string gameCode, string inviterNickname): base(windowService)
        {
            this.roomManagerClient = roomManagerClient;
            this.windowService = windowService;
            this.gameCode = gameCode;
            this.inviterNickname = inviterNickname;

            SendInvitationCommand = new RelayCommand(async (_) => await SendInvitationAsync(), (_) => CanSend());
        }

        private bool CanSend()
        {
            return !IsSending && !string.IsNullOrWhiteSpace(Email) && ValidationUtils.IsValidEmail(Email);
        }

        private async Task SendInvitationAsync()
        {
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
    }
}
