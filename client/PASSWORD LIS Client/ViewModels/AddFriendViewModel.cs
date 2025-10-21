using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.FriendsManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
                // Mostrar un PopUp basado en el 'result' (Success, UserNotFound, etc.)
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
    

    private void HandleFriendRequestResult(FriendRequestResult result)
        {
            string title = "";
            string message = "";
            PopUpIcon icon = PopUpIcon.Information;

            switch (result)
            {
                case FriendRequestResult.Success:
                    title = "Solicitud Enviada"; // Usar Langs
                    message = "Tu solicitud de amistad ha sido enviada."; // Usar Langs
                    icon = PopUpIcon.Success;
                    windowService.CloseWindow(this); // Cerramos solo si es exitoso
                    break;
                case FriendRequestResult.UserNotFound:
                    title = "Error"; // Usar Langs
                    message = "No se encontró ningún jugador con ese correo electrónico."; // Usar Langs
                    icon = PopUpIcon.Warning;
                    break;
                case FriendRequestResult.AlreadyFriends:
                case FriendRequestResult.RequestAlreadySent:
                    title = "Información"; // Usar Langs
                    message = "Ya existe una solicitud de amistad o ya eres amigo de este jugador."; // Usar Langs
                    icon = PopUpIcon.Information;
                    break;
                case FriendRequestResult.Failed:
                default:
                    title = "Error"; // Usar Langs
                    message = "No se pudo enviar la solicitud de amistad."; // Usar Langs
                    icon = PopUpIcon.Error;
                    break;
            }

            windowService.ShowPopUp(title, message, icon);
        }
    }
}
