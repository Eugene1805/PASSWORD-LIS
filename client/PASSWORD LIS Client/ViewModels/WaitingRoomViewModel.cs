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
using System.Collections.Generic;

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
                RelayCommand.RaiseCanExecuteChanged();
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
            set 
            {
                SetProperty(ref friends, value);
                UpdateFriendsMessageVisibility();
            }
        }

        private bool isLoadingFriends;
        public bool IsLoadingFriends
        {
            get => isLoadingFriends;
            set
            {
                SetProperty(ref isLoadingFriends, value);
                UpdateFriendsMessageVisibility();
            }
        }

        private bool showNoFriendsMessage;
        public bool ShowNoFriendsMessage
        {
            get => showNoFriendsMessage;
            set => SetProperty(ref showNoFriendsMessage, value);
        }

        private bool isGuest;
        public bool IsGuest
        {
            get => isGuest;
            set => SetProperty(ref isGuest, value);
        }

        
        public FriendDTO selectedFriend;
        public FriendDTO SelectedFriend 
        {
            get => selectedFriend;
            set 
            {
                SetProperty(ref selectedFriend, value);
                RelayCommand.RaiseCanExecuteChanged();
            }
        }

        private PlayerDTO currentPlayer;
        public ICommand SendMessageCommand { get; }
        public ICommand LeaveRoomCommand { get; }
        public ICommand ReportCommand { get; }
        public ICommand StartGameCommand { get; }
        public ICommand CopyGameCodeCommand { get; }
        public ICommand InviteFriendCommand { get; }
        public ICommand InviteByMailCommand { get; }

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
            InviteFriendCommand = new RelayCommand(async (_) => await InviteFriendAsync(), (_) => CanInviteFriend());
            InviteByMailCommand = new RelayCommand((_) => ShowInvitationByMail());

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
                var players = await roomManagerClient.GetPlayersInRoomAsync(this.GameCode).ConfigureAwait(false) ?? new List<PlayerDTO>();
                if (!this.IsGuest)
                {
                    this.currentPlayer = players.FirstOrDefault(p => p.Id == SessionManager.CurrentUser.PlayerId);

                    _ = LoadFriendsAsync();

                    reportManagerService.ReportReceived += OnReportReceived;
                    reportManagerService.ReportCountUpdated += OnReportCountUpdated;
                    reportManagerService.PlayerBanned += OnPlayerBanned;
                    await reportManagerService.SubscribeToReportUpdatesAsync(SessionManager.CurrentUser.PlayerId).ConfigureAwait(false);
                }
                else
                {
                    this.currentPlayer = players.FirstOrDefault(p => p.Nickname == SessionManager.CurrentUser.Nickname);
                }

                Action update = () =>
                {
                    ConnectedPlayers.Clear();
                    foreach (var player in players)
                    {
                        ConnectedPlayers.Add(player);
                    }
                    UpdatePlayerCount();
                };

                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(update);
                }
                else
                {
                    update();
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
            if (SessionManager.CurrentUser == null)
            {
                return false;
            }
            if (SessionManager.CurrentUser.PlayerId < 0)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.warningTitleText,
                    Properties.Langs.Lang.guestCantReportText, PopUpIcon.Warning);
                return false;
            }
            if (SelectedPlayer == null)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.warningTitleText,
                    Properties.Langs.Lang.noSelectedPlayerToReport, PopUpIcon.Warning);
                return false;
            }
            if(SelectedPlayer.Id == SessionManager.CurrentUser.PlayerId)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.warningTitleText,
                    Properties.Langs.Lang.cantReportYourself, PopUpIcon.Warning);
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
                        await roomManagerClient.LeaveRoomAsync(this.gameCode,
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
            RelayCommand.RaiseCanExecuteChanged();
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
                // TODO: Add navigation to game page here based on the player role and team
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
                _ = ShowSnackbarAsync(Properties.Langs.Lang.copiedToClipboardText);
            }
        }
        
        private async Task InviteFriendAsync()
        {
            var friendToInvite = SelectedFriend;
            if (friendToInvite == null)
            {
                return;
            }

            bool confirmed = windowService.ShowYesNoPopUp(Properties.Langs.Lang.confirmInvitationTitleText,
                string.Format(Properties.Langs.Lang.areSureSendingInvitation, friendToInvite.Nickname));

            if (!confirmed)
            {
                return;
            }

            try
            {
                await roomManagerClient.SendGameInvitationToFriendAsync(friendToInvite.PlayerId, gameCode, SessionManager.CurrentUser.Nickname);
                windowService.ShowPopUp(Properties.Langs.Lang.successTitleText,
                    Properties.Langs.Lang.invitationsentSuccessText, PopUpIcon.Success);
            } 
            catch (FaultException<ServiceErrorDetailDTO> ex)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    ex.Detail.Message, PopUpIcon.Error);
            } 
            catch (CommunicationException)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.networkErrorTitleText,
                    Properties.Langs.Lang.serverCommunicationErrorText, PopUpIcon.Error);
            } 
            catch (Exception)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
            }
        }
        
        private bool CanInviteFriend()
        {
            return SelectedFriend != null && !IsGuest;
        }

        private void ShowInvitationByMail()
        {
            var showInvitationMailViewModel = new InvitationByMailViewModel(App.WaitRoomManagerService, App.WindowService, this.GameCode, SessionManager.CurrentUser.Nickname);
            var invitationWindow = new InvitationByMailWindow { DataContext = showInvitationMailViewModel };
            invitationWindow.ShowDialog();
        }
        private async Task LoadFriendsAsync()
        {
            if (IsGuest || isLoadingFriends)
            {
                return;
            }

            IsLoadingFriends = true;
            try
            {
                var friendsArray = await friendsManagerService.GetFriendsAsync(SessionManager.CurrentUser.UserAccountId);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Friends.Clear();
                    if (friendsArray != null)
                    {
                        foreach (var friend in friendsArray)
                        {
                            Friends.Add(friend);
                        }
                    }
                }); 
            }
            catch (FaultException<ServiceErrorDetailDTO> ex)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    ex.Detail.Message, PopUpIcon.Error);
            }
            catch (TimeoutException)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.timeLimitTitleText,
                    Properties.Langs.Lang.serverTimeoutText, PopUpIcon.Warning);
            }
            catch (EndpointNotFoundException)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.connectionErrorTitleText,
                    Properties.Langs.Lang.serverConnectionInternetErrorText, PopUpIcon.Error);
            }
            catch (CommunicationException)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.networkErrorTitleText,
                    Properties.Langs.Lang.serverCommunicationErrorText, PopUpIcon.Error);
            }
            catch (Exception)
            {
                windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
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
                    UpdateFriendsMessageVisibility();
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
                    UpdateFriendsMessageVisibility();
                }
            });
        }
        private void UpdateFriendsMessageVisibility()
        {
            ShowNoFriendsMessage = !IsLoadingFriends && !Friends.Any();
        }
        private async Task CleanupAndUnsubscribeAsync()
        {
            if (!IsGuest)
            {
                reportManagerService.ReportReceived -= OnReportReceived;
                reportManagerService.ReportCountUpdated -= OnReportCountUpdated;
                reportManagerService.PlayerBanned -= OnPlayerBanned;
                await reportManagerService.UnsubscribeFromReportUpdatesAsync(SessionManager.CurrentUser.PlayerId);
            }
        }

        private void OnReportReceived(string reporterNickname, string reason)
        {
            lastReportReason = $"{Properties.Langs.Lang.youHaveBeenReportedByText} {reporterNickname}. {Properties.Langs.Lang.reasonText} {reason}";
        }

        private void OnReportCountUpdated(int newReportCount)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var message = $"{lastReportReason} | {Properties.Langs.Lang.currentReportsText}: {newReportCount}/3";
                _ = ShowSnackbarAsync(message);
            });
        }

        private void OnPlayerBanned(DateTime banLiftTime)
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                windowService.ShowPopUp(Properties.Langs.Lang.bannedText,
                    Properties.Langs.Lang.bannedMessageText,
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
                    Properties.Langs.Lang.hostLeftTitleText,
                    Properties.Langs.Lang.hostLeftText,
                    PopUpIcon.Warning);
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
