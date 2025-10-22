using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.FriendsManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class LobbyViewModel : BaseViewModel
    {
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

        private readonly IWindowService windowService;
        private readonly IFriendsManagerService friendsManagerService;

        public LobbyViewModel(IWindowService windowService, IFriendsManagerService friendsManagerService)
        {
            this.windowService = windowService;
            this.friendsManagerService = friendsManagerService;

            friendsManagerService.FriendRequestReceived += OnFriendRequestReceived;
            friendsManagerService.FriendAdded += OnFriendAdded;
            friendsManagerService.FriendRemoved += OnFriendRemoved;


            NavigateToProfileCommand = new RelayCommand(NavigateToProfile, (_) => !IsGuest); // Solo se puede ejecutar si NO es invitado
            Friends = new ObservableCollection<FriendDTO>();
            ViewFriendRequestsCommand = new RelayCommand(ViewFriendRequests);
            AddFriendCommand = new RelayCommand(AddFriend);
            DeleteFriendCommand = new RelayCommand(async (_) => await DeleteFriendAsync(),
                (_) => CanDeleteFriend()); 
            ShowTopPlayersCommand = new RelayCommand(ShowTopPlayers);
            HowToPlayCommand = new RelayCommand(ShowHowToPlay);
            SettingsCommand = new RelayCommand(ShowSettings);
            JoinGameCommand = new RelayCommand(JoinGame);
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
                            "No se pudo cargar la lista de amigos", PopUpIcon.Error); //
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
            MessageBoxResult result = MessageBox.Show(
            string.Format("Are you sure you want to remove {0} from your friends list?", SelectedFriend.Nickname),
            "Confirm Deletion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
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
                    _ = LoadFriendsAsync();
                    windowService.ShowPopUp("Succesful", "friend successfully deleted", PopUpIcon.Success);
                    // Actualizamos la lista en la UI al instante, sin volver a llamar al servidor
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
            var topPlayersWindow = new Views.TopPlayersWindow { DataContext = topPlayersViewModel};
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

        private void JoinGame(object parameter) 
        {
            string username = SessionManager.CurrentUser.Nickname;

            var waitingRoomViewModel = new WaitingRoomViewModel(App.WaitRoomManagerService, App.WindowService);

            var waitingRoomPage = new WaitingRoomPage(username, SessionManager.CurrentUser.PlayerId < 0)
            {
                DataContext = waitingRoomViewModel
            };

            windowService.NavigateTo(waitingRoomPage);
        }
    }
}

