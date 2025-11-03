using PASSWORD_LIS_Client.AccountManagerServiceReference;
using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.FriendsManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class LobbyViewModel : BaseViewModel
    {
        private string gameCodeToJoin;
        public string GameCodeToJoin
        {
            get => gameCodeToJoin;
            set
            {
                SetProperty(ref gameCodeToJoin, value);
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
        private int photoId;
        public int PhotoId
        {
            get => photoId; 
            set { photoId = value; OnPropertyChanged(); }
        }

        private bool isGuest;
        public bool IsGuest
        {
            get => isGuest; 
            set { isGuest = value; OnPropertyChanged(); }
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

        private FriendDTO selectedFriend;
        public FriendDTO SelectedFriend
        {
            get => selectedFriend;
            set { selectedFriend = value; OnPropertyChanged(); }
        }
        public ICommand NavigateToProfileCommand { get; }
        public ICommand ViewFriendRequestsCommand { get; }
        public ICommand AddFriendCommand { get; }
        public ICommand DeleteFriendCommand { get; }
        public ICommand ShowTopPlayersCommand { get; }
        public ICommand HowToPlayCommand { get; }
        public ICommand SettingsCommand { get; }
        public ICommand JoinGameCommand { get; }
        public ICommand CreateGameCommand { get; }

        private readonly IWindowService windowService;
        private readonly IFriendsManagerService friendsManagerService;
        private readonly IWaitingRoomManagerService waitingRoomManagerService;
        private readonly IReportManagerService reportManagerService;
        private const int GameCodeLength = 5;
        public LobbyViewModel(IWindowService windowService, IFriendsManagerService friendsManagerService, IWaitingRoomManagerService waitingRoomManagerService,
            IReportManagerService reportManagerService)
        {
            this.windowService = windowService;
            this.friendsManagerService = friendsManagerService;
            this.waitingRoomManagerService = waitingRoomManagerService;
            this.reportManagerService = reportManagerService;

            friendsManagerService.FriendRequestReceived += OnFriendRequestReceived;
            friendsManagerService.FriendAdded += OnFriendAdded;
            friendsManagerService.FriendRemoved += OnFriendRemoved;


            NavigateToProfileCommand = new RelayCommand(NavigateToProfile, (_) => !IsGuest);
            Friends = new ObservableCollection<FriendDTO>();
            ViewFriendRequestsCommand = new RelayCommand(ViewFriendRequests, (_) => !IsGuest);
            AddFriendCommand = new RelayCommand(AddFriend, (_) => !IsGuest);
            DeleteFriendCommand = new RelayCommand(async (_) => await DeleteFriendAsync(),
                (_) => CanDeleteFriend()); 
            ShowTopPlayersCommand = new RelayCommand(ShowTopPlayers);
            HowToPlayCommand = new RelayCommand(ShowHowToPlay);
            SettingsCommand = new RelayCommand(ShowSettings);
            JoinGameCommand = new RelayCommand(async (param) => await JoinGameWithCodeAsync(), (_) => CanJoinGame());
            CreateGameCommand = new RelayCommand(async (param) => await CreateGameAsync(), (_) => !IsGuest);
            LoadSessionData();

            if (SessionManager.IsUserLoggedIn() && !IsGuest)
            {
                _ = friendsManagerService.SubscribeToFriendUpdatesAsync(SessionManager.CurrentUser.UserAccountId);
            }

        }
        public void LoadSessionData()
        {
            if (!SessionManager.IsUserLoggedIn())
            {
                return;
            }
            var currentUser = SessionManager.CurrentUser;
            PhotoId = currentUser.PhotoId;
            IsGuest = currentUser.PlayerId < 0;

            if (!IsGuest)
            {
                _ = LoadFriendsAsync(); 
            }
            
        }

        private void NavigateToProfile(object parameter)
        {
            var profileViewModel = new ProfileViewModel(App.ProfileManagerService, App.WindowService);


            var page = parameter as Page;
            if (page != null)
            {
                page.NavigationService.Navigate(new ProfilePage { DataContext = profileViewModel });
            }
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
            catch (Exception) // Error genérico
            {
                windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                                        "No se pudo cargar la lista de amigos", PopUpIcon.Error); // Mensaje original
            }
            finally
            {
                IsLoadingFriends = false;
            }
        }

        private void ViewFriendRequests(object parameter)
        {
            var friendRequestsViewModel = new FriendRequestsViewModel(App.FriendsManagerService, App.WindowService);
            var friendRequestsWindow = new FriendRequestsWindow { DataContext = friendRequestsViewModel };
            friendRequestsWindow.ShowDialog();

            _ = LoadFriendsAsync();
        }
        private void AddFriend(object parameter)
        {
            var addFriendViewModel = new AddFriendViewModel(App.FriendsManagerService, App.WindowService);
            var addFriendWindow = new AddFriendWindow { DataContext = addFriendViewModel };
            addFriendWindow.ShowDialog();
 
        }

        private bool CanDeleteFriend()
        {
            return SelectedFriend != null && !IsGuest;
        }
        private async Task DeleteFriendAsync()
        {
            if (SelectedFriend == null) return;

            bool userConfirmed = windowService.ShowYesNoPopUp("Confirm Deletion", // Usar claves de recursos
                string.Format("Are you sure you want to remove {0} from your friends list?", SelectedFriend.Nickname));

            if (!userConfirmed) return;

            IsLoadingFriends = true; 
            try
            {
                bool success = await friendsManagerService.DeleteFriendAsync(
                    SessionManager.CurrentUser.PlayerId, 
                    SelectedFriend.PlayerId
                );

                if (success)
                {
                    windowService.ShowPopUp("Successful", "Friend successfully deleted", PopUpIcon.Success); // Usar claves
                }
                else
                {
                    windowService.ShowPopUp("Error", "No se pudo eliminar al amigo.", PopUpIcon.Error); // Usar claves
                }
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
            catch (Exception) 
            {
                windowService.ShowPopUp("Error", "Error de conexión al intentar eliminar al amigo.", PopUpIcon.Error); // Mensaje original
            }
            finally
            {
                IsLoadingFriends = false;
                SelectedFriend = null;
            }
        }


        private void OnFriendRequestReceived(FriendDTO requester)
        {
            //Pensar si colocar algo como indicativo visual
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
        private void ShowTopPlayers(object parameter)
        {
            var topPlayersViewModel = new TopPlayersViewModel(App.TopPlayersManagerService, App.WindowService);
            var topPlayersWindow = new TopPlayersWindow { DataContext = topPlayersViewModel};
            topPlayersWindow.ShowDialog();
        }
        private void ShowHowToPlay(object parameter)
        {
            var howToPlayWindow = new HowToPlayWindow();

            howToPlayWindow.ShowDialog();
        }

        private void ShowSettings(object parameter)
        {
            var settingsViewModel = new SettingsViewModel(App.WindowService, App.FriendsManagerService, App.BackgroundMusicService);
            var settingsWindow = new SettingsWindow { DataContext = settingsViewModel };
            settingsWindow.ShowDialog();

            if (settingsViewModel.WasLogoutSuccessful)
            {
                windowService.ShowLoginWindow();
                windowService.CloseMainWindow();
            }
        }
        private async Task CreateGameAsync()
        {
            try
            {
                bool isBanned = await reportManagerService.IsPlayerBannedAsync(SessionManager.CurrentUser.PlayerId);
                if (isBanned)
                {
                    windowService.ShowPopUp(Properties.Langs.Lang.bannedAccountText, Properties.Langs.Lang.cantCreateMatchText, PopUpIcon.Warning);
                    return;
                }
                string newGameCode = await waitingRoomManagerService.CreateGameAsync(SessionManager.CurrentUser.Email);

                if (!string.IsNullOrEmpty(newGameCode))
                {
                    var waitingRoomViewModel = new WaitingRoomViewModel(App.WaitRoomManagerService, App.WindowService, App.FriendsManagerService,
                        App.ReportManagerService);
                    await waitingRoomViewModel.InitializeAsync(newGameCode, isHost: true);

                    var waitingRoomPage = new WaitingRoomPage { DataContext = waitingRoomViewModel };
                    windowService.NavigateTo(waitingRoomPage);
                }
                else
                {
                    windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText, Properties.Langs.Lang.couldNotCreateMatch, PopUpIcon.Error);
                }
            }
            catch(FaultException<WaitingRoomManagerServiceReference.ServiceErrorDetailDTO> ex)
            {
                switch (ex.Detail.Code)
                {
                    case WaitingRoomManagerServiceReference.ServiceErrorCode.COULD_NOT_CREATE_ROOM:
                        windowService.ShowPopUp(Properties.Langs.Lang.warningTitleText,
                        Properties.Langs.Lang.couldNotCreateMatch, PopUpIcon.Warning);
                        break;
                    default:
                        windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                        Properties.Langs.Lang.unexpectedServerErrorText, PopUpIcon.Error);
                        break;
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
            catch (CommunicationException ex)
            {
                Console.WriteLine(ex.StackTrace);
                this.windowService.ShowPopUp(Properties.Langs.Lang.networkErrorTitleText,
                    Properties.Langs.Lang.serverCommunicationErrorText, PopUpIcon.Error);
            }
            catch (Exception)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                    Properties.Langs.Lang.unexpectedErrorText, PopUpIcon.Error);
            }
        }
        private bool CanJoinGame()
        {
            return !string.IsNullOrWhiteSpace(GameCodeToJoin) && GameCodeToJoin.Length == GameCodeLength;
        }
        private async Task JoinGameWithCodeAsync()
        {
            try
            {
                bool success;
                if (IsGuest)
                {
                    success = await waitingRoomManagerService.JoinGameAsGuestAsync(GameCodeToJoin, SessionManager.CurrentUser.Nickname);
                }
                else
                {
                    bool isBanned = await reportManagerService.IsPlayerBannedAsync(SessionManager.CurrentUser.PlayerId);
                    if (isBanned)
                    {
                        windowService.ShowPopUp(Properties.Langs.Lang.bannedAccountText, Properties.Langs.Lang.cantJoinMatchText, PopUpIcon.Warning);
                        return;
                    }
                    var playerId = await waitingRoomManagerService.JoinGameAsRegisteredPlayerAsync(GameCodeToJoin, SessionManager.CurrentUser.Email);
                    success = playerId > 0;
                }

                if (success)
                {

                    var waitingRoomViewModel = new WaitingRoomViewModel(App.WaitRoomManagerService, App.WindowService, App.FriendsManagerService,
                        App.ReportManagerService);
                    await waitingRoomViewModel.InitializeAsync(GameCodeToJoin, isHost: false);

                    var waitingRoomPage = new WaitingRoomPage { DataContext = waitingRoomViewModel };
                    windowService.NavigateTo(waitingRoomPage);
                }
                else 
                {
                    windowService.ShowPopUp(Properties.Langs.Lang.joinFailedTitle,
                        Properties.Langs.Lang.unexpectedServerErrorText, PopUpIcon.Warning);
                }
            }
            catch(FaultException<WaitingRoomManagerServiceReference.ServiceErrorDetailDTO> ex)
            {
                switch (ex.Detail.Code)
                {
                    case WaitingRoomManagerServiceReference.ServiceErrorCode.ROOM_NOT_FOUND:
                        windowService.ShowPopUp(Properties.Langs.Lang.joinFailedTitle,
                        Properties.Langs.Lang.incorrectCodeText, PopUpIcon.Warning);
                        break;
                    case WaitingRoomManagerServiceReference.ServiceErrorCode.ROOM_FULL:
                        windowService.ShowPopUp(Properties.Langs.Lang.joinFailedTitle,
                        Properties.Langs.Lang.roomFullText, PopUpIcon.Warning);
                        break;
                    case WaitingRoomManagerServiceReference.ServiceErrorCode.ALREADY_IN_ROOM:
                        windowService.ShowPopUp(Properties.Langs.Lang.joinFailedTitle,
                        Properties.Langs.Lang.youAreAlreadyInGameText, PopUpIcon.Warning);
                        break;
                    default:
                        windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                        Properties.Langs.Lang.unexpectedServerErrorText, PopUpIcon.Error);
                        break;
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
    }
}

