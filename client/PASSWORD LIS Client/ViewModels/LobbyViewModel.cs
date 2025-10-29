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
                ((RelayCommand)JoinGameCommand).RaiseCanExecuteChanged();
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

        //Propiedad para la lista de amigos
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
            ViewFriendRequestsCommand = new RelayCommand(ViewFriendRequests);
            AddFriendCommand = new RelayCommand(AddFriend);
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
            /*
            if (SessionManager.CurrentUser.PlayerId<0)
            {
                return;
            }
            */
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

        private void ViewFriendRequests(object parameter)
        {
            var friendRequestsViewModel = new FriendRequestsViewModel(App.FriendsManagerService);
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
            bool userConfirmed = windowService.ShowYesNoPopUp("Confirm Deletion",
                string.Format("Are you sure you want to remove {0} from your friends list?", SelectedFriend.Nickname));
           

            if (!userConfirmed)
            {
                return; 
            }

            try
            {
                bool success = await friendsManagerService.DeleteFriendAsync(
                    SessionManager.CurrentUser.PlayerId,
                    SelectedFriend.PlayerId
                );

                if (success)
                {
                    //_ = LoadFriendsAsync();
                    windowService.ShowPopUp("Succesful", "friend successfully deleted", PopUpIcon.Success);
                }
                else
                {
                    windowService.ShowPopUp("Error", "No se pudo eliminar al amigo.", PopUpIcon.Error);
                }
            }
            catch (Exception)
            {
                windowService.ShowPopUp("Error", "Error de conexión al intentar eliminar al amigo.", PopUpIcon.Error);
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
            var howToPlayViewModel = new HowToPlayViewModel();
            var howToPlayWindow = new HowToPlayWindow { DataContext = howToPlayViewModel };

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
                    windowService.ShowPopUp("Cuenta Suspendida", "No puedes crear partidas, tu cuenta está suspendida temporalmente.", PopUpIcon.Warning);
                    return; // Detiene la ejecución
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
                    // TODO Add lang message
                    windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText, "No se pudo crear la partida.", PopUpIcon.Error);
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
                        windowService.ShowPopUp("Cuenta Suspendida", "No puedes crear partidas, tu cuenta está suspendida temporalmente.", PopUpIcon.Warning);
                        return; // Detiene la ejecución
                    }
                    success = await waitingRoomManagerService.JoinGameAsRegisteredPlayerAsync(GameCodeToJoin, SessionManager.CurrentUser.Email);
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
                {// TODO ADD lang messages and catch specific errors
                    windowService.ShowPopUp("Unión Fallida", "El código de la partida es incorrecto, la sala está llena o ya estás en la partida.", PopUpIcon.Warning);
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

