using PASSWORD_LIS_Client.AccountManagerServiceReference;
using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.FriendsManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
<<<<<<< HEAD
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
=======
>>>>>>> f328f7cfcf47b0aaa98e2c3f2696e424021d8e5f
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
            set { email = value; OnPropertyChanged(); }
        }

        private bool isSending;
        public bool IsSending
        {
            get => isSending;
            set { isSending = value; OnPropertyChanged(); }
        }

        public ICommand SendRequestCommand { get; }

        private readonly IFriendsManagerService friendsService;
        private readonly IWindowService windowService;

        public AddFriendViewModel(IFriendsManagerService friendsService, IWindowService windowService)
        {
            this.friendsService = friendsService;
            this.windowService = windowService;
            SendRequestCommand = new RelayCommand(async (_) => await SendRequestAsync(), (_) => !IsSending && !string.IsNullOrWhiteSpace(Email));
        }

        private async Task SendRequestAsync()
        {
            IsSending = true;
            try
            {
                var result = await friendsService.SendFriendRequestAsync(Email);
                HandleFriendRequestResult(result);
            }
            catch (FaultException<ServiceErrorDetailDTO> ex)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText, ex.Detail.Message, PopUpIcon.Error);
            }
            catch (TimeoutException)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.timeLimitTitleText, Properties.Langs.Lang.serverTimeoutText, PopUpIcon.Warning);
            }
            catch (EndpointNotFoundException)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.connectionErrorTitleText, Properties.Langs.Lang.serverConnectionInternetErrorText, PopUpIcon.Error);
            }
            catch (CommunicationException)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.networkErrorTitleText, Properties.Langs.Lang.serverCommunicationErrorText, PopUpIcon.Error);
            }
            catch (System.Exception) // Captura genérica original
            {
                windowService.ShowPopUp("Error", "Error de conexión", PopUpIcon.Error); // Mensaje original
            }
            finally
            {
                IsSending = false;
            }
        }

        /*
        private async Task SendRequestAsync()
        {
            IsSending = true;
            try
            {
                var result = await friendsService.SendFriendRequestAsync(Email);
                HandleFriendRequestResult(result);
            }
            catch (System.Exception)
            {
                windowService.ShowPopUp("Error", "Error de conexión", PopUpIcon.Error);
            }
            finally
            {
                IsSending = false;
            }
        }        
         */


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
                    windowService.ShowPopUp("Información",
                        "Ya tienes una amistad con este jugador", PopUpIcon.Information);
                    break;
                case FriendRequestResult.RequestAlreadySent:
                    windowService.ShowPopUp("Información",
                        "Ya has enviado una soicitud a este jugador", PopUpIcon.Information);
                    break;
                case FriendRequestResult.CannotAddSelf:
                    windowService.ShowPopUp(Properties.Langs.Lang.informationText,
                        "No puedes enviarte una solicitud de amistas a ti mismo.",
                        PopUpIcon.Information);
                    break;
                case FriendRequestResult.RequestAlreadyReceived:
                    windowService.ShowPopUp("Información",
                        "Este jugador ya te ha enviado una solicitud de amistad, revisa tu bandeja", PopUpIcon.Information);
                    break;
                case FriendRequestResult.Failed:
                default:
                    windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                        Properties.Langs.Lang.couldNotSentRequestText, PopUpIcon.Error);
                    break;
            }
        }
    }
}
