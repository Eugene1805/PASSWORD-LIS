using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using PASSWORD_LIS_Client.WaitingRoomManagerServiceReference;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class WaitingRoomViewModel : BaseViewModel
    {
        private readonly IWaitingRoomManagerService roomManagerClient;
        private readonly IWindowService windowService;
        public ObservableCollection<string> ChatMessages { get; }
        public ObservableCollection<PlayerDTO> ConnectedPlayers { get; }

        private string currentMessage;
        public string CurrentMessage
        {
            get => currentMessage;
            set => SetProperty(ref currentMessage, value);
        }

        public ICommand SendMessageCommand { get; }
        public WaitingRoomViewModel(IWaitingRoomManagerService roomManagerService, IWindowService windowService)
        {
            this.roomManagerClient = roomManagerService;
            this.windowService = windowService;

            ChatMessages = new ObservableCollection<string>();
            ConnectedPlayers = new ObservableCollection<PlayerDTO>();

            SendMessageCommand = new RelayCommand(async (_) => await SendMessageAsync(),(_) => CanSendMessage());

            // 1. Suscribirse a los eventos del servicio
            // Necesitas castear si la interfaz no expone los eventos directamente
            if (roomManagerClient is WcfWaitingRoomManagerService wcfService)
            {
                wcfService.MessageReceived += OnMessageReceived;
                wcfService.PlayerJoined += OnPlayerJoined;
                wcfService.PlayerLeft += OnPlayerLeft;
            }

        }

        public async Task LoadInitialData()
        {
            await LoadInitialDataAsync();
        }

        private async Task LoadInitialDataAsync()
        {
            try
            {
                string username = SessionManager.CurrentUser.Nickname; // Obtener esto de tu gestor de sesión
                bool joined = await roomManagerClient.JoinAsRegisteredPlayerAsync(username);
                if (joined)
                {
                    var players = await roomManagerClient.GetConnectedPlayersAsync();
                    foreach (var player in players)
                    {
                        ConnectedPlayers.Add(player);
                    }
                }
                else
                {
                    windowService.ShowPopUp("No se pudo unir a la sala. ", "El usuario ya podría estar conectado.", PopUpIcon.Warning);
                }
            }
            catch (Exception ex)
            {

                Console.WriteLine("Error al cargar datos iniciales de la sala de espera.");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
            
        }

        // --- Lógica de los Comandos ---
        private bool CanSendMessage() => !string.IsNullOrWhiteSpace(CurrentMessage);
        private async Task SendMessageAsync()
        {
            await roomManagerClient.JoinAsRegisteredPlayerAsync(CurrentMessage);
            CurrentMessage = string.Empty;
        }

        // --- Manejadores de Eventos del Servicio (Callbacks) ---

        private void OnMessageReceived(ChatMessage message)
        {
            // Usa el Dispatcher para actualizar la UI desde cualquier hilo
            Application.Current.Dispatcher.Invoke(() =>
            {
                ChatMessages.Add($"{message.SenderUsername}: {message.Message}");
            });
        }

        private void OnPlayerJoined(PlayerDTO player)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ConnectedPlayers.Add(player);
            });
        }

        private void OnPlayerLeft(int playerId)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var playerToRemove = ConnectedPlayers.FirstOrDefault(p => p.Id == playerId);
                if (playerToRemove != null)
                {
                    ConnectedPlayers.Remove(playerToRemove);
                }
            });
        }
    }
}
