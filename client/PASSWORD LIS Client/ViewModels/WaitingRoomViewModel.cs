using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using PASSWORD_LIS_Client.WaitingRoomManagerServiceReference;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.ServiceModel;
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

        private PlayerDTO selectedPlayer;
        public PlayerDTO SelectedPlayer
        {
            get => selectedPlayer;
            set
            {
                SetProperty(ref selectedPlayer, value);
                ((RelayCommand)ReportCommand).RaiseCanExecuteChanged();
            }
        }
        private string currentMessage;
        public string CurrentMessage
        {
            get => currentMessage;
            set => SetProperty(ref currentMessage, value);
        }
        private bool isSnackbarVisible;
        public bool IsSnackbarVisible
        {
            get => isSnackbarVisible;
            set => SetProperty(ref isSnackbarVisible, value);
        }

        private string snackbarMessage;
        public string SnackbarMessage
        {
            get => snackbarMessage;
            set => SetProperty(ref snackbarMessage, value);
        }
        private PlayerDTO currentPlayer;
        public ICommand SendMessageCommand { get; }
        public ICommand LeaveRoomCommand { get; }
        public ICommand ReportCommand { get; }
        public WaitingRoomViewModel(IWaitingRoomManagerService roomManagerService, IWindowService windowService)
        {
            this.roomManagerClient = roomManagerService;
            this.windowService = windowService;

            ChatMessages = new ObservableCollection<string>();
            ConnectedPlayers = new ObservableCollection<PlayerDTO>();

            SendMessageCommand = new RelayCommand(async (_) => await SendMessageAsync(),(_) => CanSendMessage());
            LeaveRoomCommand = new RelayCommand(async (_) => await LeaveRoomAsync());
            ReportCommand = new RelayCommand(
            execute: (_) => OpenReportWindow(),
            canExecute: (_) => CanReportPlayer()
            );  

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

                if (isGuest)
                {
                    Console.WriteLine($"Intentando unirse como invitado: {username}");
                    joined = await roomManagerClient.JoinAsGuestAsync(username);
                }
                else
                {
                    Console.WriteLine($"Intentando unirse como jugador registrado: {username}");
                    joined = await roomManagerClient.JoinAsRegisteredPlayerAsync(SessionManager.CurrentUser.Email);
                }

                if (joined)
                {
                    var players = await roomManagerClient.GetConnectedPlayersAsync();
                    this.currentPlayer = players.FirstOrDefault(p => p.Nickname == username);

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
                    windowService.ShowPopUp(Properties.Langs.Lang.couldNotJoinText,
                        Properties.Langs.Lang.nicknameInUseText, PopUpIcon.Warning);
                }
            }
            catch (TimeoutException)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.timeLimitTitleText,
                    Properties.Langs.Lang.serverTimeoutText, PopUpIcon.Warning);
            }
            catch (EndpointNotFoundException)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.connectionErrorTitleText,
                    Properties.Langs.Lang.serverConnectionInternetErrorText, PopUpIcon.Error);
            }
            catch (CommunicationException)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.networkErrorTitleText,
                    Properties.Langs.Lang.serverCommunicationErrorText, PopUpIcon.Error);
            }
            catch (Exception)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
            }
        }
        private bool CanReportPlayer()
        {
            return SelectedPlayer != null;
        }

        private void OpenReportWindow()
        {
            if (SelectedPlayer != null)
            {
                windowService.ShowReportWindow(SelectedPlayer);
            }
        }
        private bool CanSendMessage()
        {
            return !string.IsNullOrWhiteSpace(CurrentMessage);
        }
        private async Task SendMessageAsync()
        {
            await roomManagerClient.SendMessageAsync(CurrentMessage);
            CurrentMessage = string.Empty;
        }
        private async Task LeaveRoomAsync()
        {
            try
            {
                if (this.currentPlayer != null)
                {
                    await roomManagerClient.LeaveRoomAsync(this.currentPlayer.Id);
                }
            }
            catch (TimeoutException)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.timeLimitTitleText,
                    Properties.Langs.Lang.serverTimeoutText, PopUpIcon.Warning);
            }
            catch (EndpointNotFoundException)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.connectionErrorTitleText,
                    Properties.Langs.Lang.serverConnectionInternetErrorText, PopUpIcon.Error);
            }
            catch (CommunicationException)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.networkErrorTitleText,
                    Properties.Langs.Lang.serverCommunicationErrorText, PopUpIcon.Error);
            }
            catch (Exception)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
            }
            finally
            {
                windowService.GoBack();
            }
        }

        private void OnMessageReceived(ChatMessage message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ChatMessages.Add($"{message.SenderNickname}: {message.Message}");
            });
        }

        private void OnPlayerJoined(PlayerDTO player)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!ConnectedPlayers.Any(p => p.Id == player.Id))
                {
                    ConnectedPlayers.Add(player);
                    _ = ShowSnackbarAsync($"{player.Nickname} joined");
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
                    _ = ShowSnackbarAsync($"{playerToRemove.Nickname} left");
                }
            });
        }

        private async Task ShowSnackbarAsync(string message)
        {
            SnackbarMessage = message;
            IsSnackbarVisible = true;

            await Task.Delay(3000);

            IsSnackbarVisible = false;
        }
    }
}
