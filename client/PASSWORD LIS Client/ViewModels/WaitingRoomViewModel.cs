using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using PASSWORD_LIS_Client.WaitingRoomManagerServiceReference;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
        private PlayerDTO currentPlayer;
        public ICommand SendMessageCommand { get; }
        public ICommand LeaveRoomCommand { get; }
        public WaitingRoomViewModel(IWaitingRoomManagerService roomManagerService, IWindowService windowService)
        {
            this.roomManagerClient = roomManagerService;
            this.windowService = windowService;

            ChatMessages = new ObservableCollection<string>();
            ConnectedPlayers = new ObservableCollection<PlayerDTO>();

            SendMessageCommand = new RelayCommand(async (_) => await SendMessageAsync(),(_) => CanSendMessage());
            LeaveRoomCommand = new RelayCommand(async (_) => await LeaveRoomAsync(_));
            // 1. Suscribirse a los eventos del servicio
            // Necesitas castear si la interfaz no expone los eventos directamente
            if (roomManagerClient is WcfWaitingRoomManagerService wcfService)
            {
                wcfService.MessageReceived += OnMessageReceived;
                wcfService.PlayerJoined += OnPlayerJoined;
                wcfService.PlayerLeft += OnPlayerLeft;
            }

        }

        public async Task LoadInitialDataAsync(string username, bool isGuest)
        {
            try
            {
                bool joined = false;

                // CAMBIO 2: Lógica condicional para llamar al método correcto
                if (isGuest)
                {
                    Console.WriteLine($"Intentando unirse como invitado: {username}");
                    joined = await roomManagerClient.JoinAsGuestAsync(username);
                }
                else
                {
                    Console.WriteLine($"Intentando unirse como jugador registrado: {username}");
                    joined = await roomManagerClient.JoinAsRegisteredPlayerAsync(username);
                }

                if (joined)
                {
                    var players = await roomManagerClient.GetConnectedPlayersAsync();
                    this.currentPlayer = players.FirstOrDefault(p => p.Username == username);

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ConnectedPlayers.Clear();
                        foreach (var player in players)
                        {
                            ConnectedPlayers.Add(player);
                        }
                    });
                }
                else
                {
                    windowService.ShowPopUp("No se pudo unir a la sala.", "El nombre de usuario ya podría estar en uso.", PopUpIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al cargar datos iniciales de la sala de espera.");
                Console.WriteLine(ex.Message);
            }
        }
        // --- Lógica de los Comandos ---
        private bool CanSendMessage() => !string.IsNullOrWhiteSpace(CurrentMessage);
        private async Task SendMessageAsync()
        {
            await roomManagerClient.SendMessageAsync(CurrentMessage);
            CurrentMessage = string.Empty;
        }
        private async Task LeaveRoomAsync(object parameter)
        {
            try
            {
                // Notificamos al servidor solo si tenemos un jugador válido
                if (this.currentPlayer != null)
                {
                    await roomManagerClient.LeaveRoomAsync(this.currentPlayer.Id);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al intentar salir de la sala: {ex.Message}");
                // Aunque falle, igual intentamos navegar hacia atrás
            }
            finally
            {
                // Usamos el servicio de ventanas para regresar a la página anterior
                windowService.GoBack();
            }
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
                // Evitar añadir duplicados si ya está en la lista
                if (!ConnectedPlayers.Any(p => p.Id == player.Id))
                {
                    ConnectedPlayers.Add(player);
                }
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
