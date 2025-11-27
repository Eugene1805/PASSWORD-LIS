using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.FriendsManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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

        private FriendDTO selectedFriend;
        public FriendDTO SelectedFriend
        {
            get => selectedFriend;
            set { selectedFriend = value; OnPropertyChanged(); }
        }

        private bool showNoFriendsMessage;
        public bool ShowNoFriendsMessage
        {
            get => showNoFriendsMessage;
            set { SetProperty(ref showNoFriendsMessage, value); }
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

        private readonly IFriendsManagerService friendsManagerService;
        private readonly IWaitingRoomManagerService waitingRoomManagerService;
        private readonly IReportManagerService reportManagerService;
        private const int GameCodeLength = 5;
        
        public LobbyViewModel(IWindowService windowService, IFriendsManagerService friendsManagerService, 
            IWaitingRoomManagerService waitingRoomManagerService,IReportManagerService reportManagerService)
            : base(windowService)
        {
            this.friendsManagerService = friendsManagerService;
            this.waitingRoomManagerService = waitingRoomManagerService;
            this.reportManagerService = reportManagerService;

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
            var profilePage = new ProfilePage { DataContext = profileViewModel };
            windowService.NavigateTo(profilePage);
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
                await ExecuteAsync(async () =>
                {
                    var friendsArray = await friendsManagerService.GetFriendsAsync(
                        SessionManager.CurrentUser.UserAccountId);
                    await Application.Current.Dispatcher.InvokeAsync(() =>
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
                });
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
            var friendToDelete = SelectedFriend;
            if (friendToDelete == null)
            {
                return;
            }
            bool userConfirmed = windowService.ShowYesNoPopUp(Properties.Langs.Lang.deletionConfirmationTitleText,
                string.Format(Properties.Langs.Lang.deletionConfirmationText, SelectedFriend.Nickname));

            if (!userConfirmed)
            {
                return;
            }
            IsLoadingFriends = true; 
            try 
            {
                await ExecuteAsync(async () =>
                {
                    bool success = await friendsManagerService.DeleteFriendAsync(
                        SessionManager.CurrentUser.PlayerId,
                        friendToDelete.PlayerId
                    );

                    if (success)
                    {
                        windowService.ShowPopUp(Properties.Langs.Lang.successTitleText,
                            Properties.Langs.Lang.successDeletionText, PopUpIcon.Success);
                    }
                    else
                    {
                        windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                            Properties.Langs.Lang.couldNotRemoveFriendTitleText, PopUpIcon.Error);
                    }
                });
            }
            finally
            {
                IsLoadingFriends = false;
                SelectedFriend = null;
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
        private void ShowTopPlayers(object parameter)
        {
            var topPlayersViewModel = new TopPlayersViewModel(App.TopPlayersManagerService, App.WindowService);
            var topPlayersWindow = new TopPlayersWindow { DataContext = topPlayersViewModel};
            topPlayersWindow.ShowDialog();
        }
        private static void ShowHowToPlay(object parameter)
        {
            var howToPlayWindow = new HowToPlayWindow();
            howToPlayWindow.ShowDialog();
        }

        private void ShowSettings(object parameter)
        {
            var settingsViewModel = new SettingsViewModel(App.WindowService, 
                App.FriendsManagerService, App.BackgroundMusicService);
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
            await ExecuteAsync(async () =>
            {
                bool isBanned = await reportManagerService.IsPlayerBannedAsync(SessionManager.CurrentUser.PlayerId);
                if (isBanned)
                {
                    windowService.ShowPopUp(Properties.Langs.Lang.bannedAccountText,
                        Properties.Langs.Lang.cantCreateMatchText, PopUpIcon.Warning);
                    return;
                }
                string newGameCode = await waitingRoomManagerService.CreateRoomAsync(SessionManager.CurrentUser.Email);

                if (!string.IsNullOrEmpty(newGameCode))
                {
                    var waitingRoomViewModel = new WaitingRoomViewModel(App.WaitRoomManagerService,
                        App.WindowService, App.FriendsManagerService,
                        App.ReportManagerService);
                    await waitingRoomViewModel.InitializeAsync(newGameCode, isHost: true);

                    if (Application.Current == null || Application.Current.Dispatcher == null)
                    {
                        windowService.NavigateTo(null);
                    }
                    else
                    {
                        var waitingRoomPage = new WaitingRoomPage { DataContext = waitingRoomViewModel };
                        windowService.NavigateTo(waitingRoomPage);
                    }
                }
                else
                {
                    windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                        Properties.Langs.Lang.couldNotCreateMatch, PopUpIcon.Error);
                }
            });
        }
        private bool CanJoinGame()
        {
            return !string.IsNullOrWhiteSpace(GameCodeToJoin) && GameCodeToJoin.Length == GameCodeLength;
        }
        private async Task JoinGameWithCodeAsync()
        {
            await ExecuteAsync(async () =>
            {
                bool success;
                if (IsGuest)
                {
                    success = await waitingRoomManagerService.JoinRoomAsGuestAsync(
                        GameCodeToJoin, SessionManager.CurrentUser.Nickname);
                }
                else
                {
                    bool isBanned = await reportManagerService.IsPlayerBannedAsync(
                        SessionManager.CurrentUser.PlayerId);
                    if (isBanned)
                    {
                        windowService.ShowPopUp(Properties.Langs.Lang.bannedAccountText, 
                            Properties.Langs.Lang.cantJoinMatchText, PopUpIcon.Warning);
                        return;
                    }
                    var playerId = await waitingRoomManagerService.JoinRoomAsRegisteredPlayerAsync(
                        GameCodeToJoin, SessionManager.CurrentUser.Email);
                    success = playerId > 0;
                }

                if (success)
                {

                    var waitingRoomViewModel = new WaitingRoomViewModel(App.WaitRoomManagerService,
                        App.WindowService, App.FriendsManagerService,App.ReportManagerService);
                    await waitingRoomViewModel.InitializeAsync(GameCodeToJoin, isHost: false);

                    if (Application.Current == null || Application.Current.Dispatcher == null)
                    {
                        windowService.NavigateTo(null);
                    }
                    else
                    {
                        var waitingRoomPage = new WaitingRoomPage { DataContext = waitingRoomViewModel };
                        windowService.NavigateTo(waitingRoomPage);
                    }
                }
                else
                {
                    windowService.ShowPopUp(Properties.Langs.Lang.joinFailedTitle,
                        Properties.Langs.Lang.unexpectedServerErrorText, PopUpIcon.Warning);
                }
            });
        }
    }
}

