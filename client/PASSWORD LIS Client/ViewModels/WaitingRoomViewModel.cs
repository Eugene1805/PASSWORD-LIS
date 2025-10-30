using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.FriendsManagerServiceReference;
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
        public ObservableCollection<string> ChatMessages { get; }
        public ObservableCollection<PlayerDTO> ConnectedPlayers { get; }

        private string gameCode;
        public string GameCode
        {
            get => gameCode;
            set => SetProperty(ref gameCode, value);
        }
        private bool isHost;
        public bool IsHost
        {
            get => isHost;
            set => SetProperty(ref isHost, value);
        }
        private string playerCountText;
        public string PlayerCountText
        {
            get => playerCountText;
            set => SetProperty(ref playerCountText, value);
        }

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

        private ObservableCollection<FriendDTO> friends;
        public ObservableCollection<FriendDTO> Friends
        {
            get => friends;
            set { friends = value; OnPropertyChanged(); }
        }

        private bool isLoadingFriends;
        public bool IsLoadingFriends
        {
            get => isLoadingFriends;
            set { isLoadingFriends = value; OnPropertyChanged(); }
        }

        private bool isGuest;
        public bool IsGuest
        {
            get => isGuest;
            set => SetProperty(ref isGuest, value);
        }

        /* DESCOMENTAR CUANDO SE IMPLEMENTE LO DE INVITAR AMIGOS
        public FriendDTO selectedFriend;
        public FriendDTO SelectedFriend 
        {
            get => selectedFriend;
            set { selectedFriend = value; OnPropertyChanged(); }
        }
         */

        private PlayerDTO currentPlayer;
        public ICommand SendMessageCommand { get; }
        public ICommand LeaveRoomCommand { get; }
        public ICommand ReportCommand { get; }
        public ICommand StartGameCommand { get; }
        public ICommand CopyGameCodeCommand { get; }

        private readonly IWaitingRoomManagerService roomManagerClient;
        private readonly IWindowService windowService;
        private readonly IFriendsManagerService friendsManagerService;
        private readonly IReportManagerService reportManagerService;
        private const int MaxPlayers = 4;
        private string lastReportReason;
        public WaitingRoomViewModel(IWaitingRoomManagerService roomManagerService, IWindowService windowService, IFriendsManagerService friendsManagerService,
            IReportManagerService reportManagerService) 
        {
            this.roomManagerClient = roomManagerService;
            this.windowService = windowService;
            this.friendsManagerService = friendsManagerService;
            this.reportManagerService = reportManagerService;
            friendsManagerService.FriendAdded += OnFriendAdded;
            friendsManagerService.FriendRemoved += OnFriendRemoved;

            ChatMessages = new ObservableCollection<string>();
            ConnectedPlayers = new ObservableCollection<PlayerDTO>();
            Friends = new ObservableCollection<FriendDTO>();

            SendMessageCommand = new RelayCommand(async (_) => await SendMessageAsync(),(_) => CanSendMessage());
            LeaveRoomCommand = new RelayCommand(async (_) => await LeaveGameAsync());
            ReportCommand = new RelayCommand( (_) => OpenReportWindow());
            StartGameCommand = new RelayCommand(async (_) => await StartGameAsync(), (_) => CanStartGame());
            CopyGameCodeCommand = new RelayCommand((_) => CopyGameCodeToClipboard());

            if (roomManagerClient is WcfWaitingRoomManagerService wcfService)
            {
                wcfService.MessageReceived += OnMessageReceived;
                wcfService.PlayerJoined += OnPlayerJoined;
                wcfService.PlayerLeft += OnPlayerLeft;
                wcfService.GameStarted += OnGameStarted;
                wcfService.HostLeft += OnHostLeft;
            }


        }

        public async Task InitializeAsync(string gameCode, bool isHost)
        {
            this.GameCode = gameCode;
            this.IsHost = isHost;
            this.IsGuest = SessionManager.CurrentUser == null || SessionManager.CurrentUser.PlayerId < 0;

            try
            {
                var players = await roomManagerClient.GetPlayersInGameAsync(this.GameCode);
                if (!this.IsGuest)
                {
                    this.currentPlayer = players.FirstOrDefault(p => p.Id == SessionManager.CurrentUser.PlayerId);
                    _ = LoadFriendsAsync();
                    reportManagerService.ReportReceived += OnReportReceived;
                    reportManagerService.ReportCountUpdated += OnReportCountUpdated;
                    reportManagerService.PlayerBanned += OnPlayerBanned;
                    await reportManagerService.SubscribeToReportUpdatesAsync(SessionManager.CurrentUser.PlayerId);
                }
                else
                {
                    this.currentPlayer = players.FirstOrDefault(p => p.Nickname == SessionManager.CurrentUser.Nickname);
                }
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ConnectedPlayers.Clear();
                    foreach (var player in players)
                    {
                        ConnectedPlayers.Add(player);
                    }
                    UpdatePlayerCount();
                });              
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
        // TODO: Add internacionzation messages
        private bool CanReportPlayer()
        {
            if (SessionManager.CurrentUser == null)
            {
                return false;
            }
            if (SessionManager.CurrentUser.PlayerId < 0)
            {
                Console.WriteLine("Guests can not report players");
                return false;
            }
            if (SelectedPlayer == null)
            {
                Console.WriteLine("You need to select a player from the conected ones to generate a report");
                return false;
            }
            if(SelectedPlayer.Id == SessionManager.CurrentUser.PlayerId)
            {
                Console.WriteLine("You can not report yourself");
                return false;
            }
            return  true;
        }

        private void OpenReportWindow()
        {
            if (CanReportPlayer())
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
            try
            {
                await roomManagerClient.SendMessageAsync(this.gameCode, new ChatMessageDTO
                {
                    Message = CurrentMessage,
                    SenderNickname = SessionManager.CurrentUser.Nickname
                });
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
                CurrentMessage = string.Empty;
            }
            
        }
        private async Task LeaveGameAsync()
        {
            try
            {
                if (this.currentPlayer != null)
                {
                    if (IsHost)
                    {
                        await roomManagerClient.HostLeftAsync(this.gameCode);
                    }
                    else
                    {
                        await roomManagerClient.LeaveGameAsync(this.gameCode,
                            IsGuest ? currentPlayer.Id : SessionManager.CurrentUser.PlayerId);
                    }
                }
                if(!IsGuest)
                {
                    await CleanupAndUnsubscribeAsync();
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
        private void UpdatePlayerCount()
        {
            PlayerCountText = $"{ConnectedPlayers.Count}/{MaxPlayers}";
            ((RelayCommand)StartGameCommand).RaiseCanExecuteChanged();
        }
        private async Task StartGameAsync()
        {
            try
            {
                await roomManagerClient.StartGameAsync(GameCode);
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

        private bool CanStartGame()
        {
            return IsHost && ConnectedPlayers.Count == MaxPlayers;
        }
        private void OnGameStarted()
        {
            if (!IsGuest)
            {
                _ = CleanupAndUnsubscribeAsync();
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                //Add navigation to game page here based on the player role and team
                windowService.NavigateTo(new ClueGuyPage());
            });
        }
        private void OnMessageReceived(ChatMessageDTO message)
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
                    UpdatePlayerCount();
                    var joinedText = Properties.Langs.Lang.joinedText;
                    _ = ShowSnackbarAsync($"{player.Nickname} {joinedText}");
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
                    UpdatePlayerCount();
                    var leftText = Properties.Langs.Lang.leftText;
                    _ = ShowSnackbarAsync($"{playerToRemove.Nickname} {leftText}");
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
        private void CopyGameCodeToClipboard()
        {
            if (!string.IsNullOrEmpty(GameCode))
            {
                Clipboard.SetText(GameCode);

                _ = ShowSnackbarAsync("¡Código copiado al portapapeles!");
            }
        }
        private async Task LoadFriendsAsync()
        {
            if (isLoadingFriends)
            {
                return;
            }
            IsLoadingFriends = true;
            try
            {
                var friendsArray = await friendsManagerService.GetFriendsAsync(SessionManager.CurrentUser.UserAccountId);
                Friends = new ObservableCollection<FriendDTO>(friendsArray);
            }
            catch (Exception)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
            "No se pudo cargar la lista de amigos", PopUpIcon.Error);
            }
            finally
            {
                IsLoadingFriends = false;
            }
        }

        private void OnFriendAdded(FriendDTO newFriend)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (!Friends.Any(f => f.PlayerId == newFriend.PlayerId))
                {
                    Friends.Add(newFriend);
                }
            });
        }

        private void OnFriendRemoved(int friendPlayerId)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var friendToRemove = Friends.FirstOrDefault(f => f.PlayerId == friendPlayerId);
                if (friendToRemove != null)
                {
                    Friends.Remove(friendToRemove);
                }
            });
        }

        private async Task CleanupAndUnsubscribeAsync()
        {
            if (!IsGuest)
            {
                // Quitamos los manejadores para evitar fugas de memoria
                reportManagerService.ReportReceived -= OnReportReceived;
                reportManagerService.ReportCountUpdated -= OnReportCountUpdated;
                reportManagerService.PlayerBanned -= OnPlayerBanned;
                // Notificamos al servidor que ya no necesitamos las actualizaciones
                await reportManagerService.UnsubscribeFromReportUpdatesAsync(SessionManager.CurrentUser.PlayerId);
            }
        }

        private void OnReportReceived(string reporterNickname, string reason)
        {
            lastReportReason = $"Has sido reportado por {reporterNickname}. Razón: {reason}";
        }

        private void OnReportCountUpdated(int newReportCount)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var message = $"{lastReportReason} | Reportes actuales: {newReportCount}/3";
                _ = ShowSnackbarAsync(message);
            });
        }

        private void OnPlayerBanned(DateTime banLiftTime)
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                windowService.ShowPopUp("Has sido suspendido",
                    "Has acumulado 3 reportes y tu cuenta ha sido suspendida por 1 hora. Serás expulsado de la sala.",
                    PopUpIcon.Error);

                await LeaveGameAsync();
            });
        }

        private void OnHostLeft()
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                if (!isHost)
                {
                    windowService.ShowPopUp(
                    "Host abandonó la partida",
                    "El host ha abandonado la sala. La partida ha sido cancelada.",
                    PopUpIcon.Warning
                );
                }
                
                if (!IsGuest)
                {
                    await CleanupAndUnsubscribeAsync();
                }

                windowService.GoBack();
            });
        }

    }
}
