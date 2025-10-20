using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.FriendsManagerServiceReference;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System;
using System.Collections.ObjectModel;
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
        public ICommand AddFriendCommand { get; }
        public ICommand DeleteFriendCommand { get; }
        public ICommand ShowTopPlayersCommand { get; }
        public ICommand HowToPlayCommand { get; }
        public ICommand SettingsCommand { get; }
        public ICommand JoinGameCommand { get; }

        private readonly IWindowService windowService;
        private readonly IFriendsManagerService friendsManagerService;

        private bool friendsLoaded = false;
        public LobbyViewModel(IWindowService windowService, IFriendsManagerService friendsManagerService)
        {
            this.windowService = windowService;
            this.friendsManagerService = friendsManagerService;

            NavigateToProfileCommand = new RelayCommand(NavigateToProfile, (_) => !IsGuest); // Solo se puede ejecutar si NO es invitado
            Friends = new ObservableCollection<FriendDTO>();
            AddFriendCommand = new RelayCommand(AddFriend);
            DeleteFriendCommand = new RelayCommand(async (_) => await DeleteFriendAsync(),
                (_) => CanDeleteFriend()); 
            ShowTopPlayersCommand = new RelayCommand(ShowTopPlayers);
            HowToPlayCommand = new RelayCommand(ShowHowToPlay);
            SettingsCommand = new RelayCommand(ShowSettings);
            JoinGameCommand = new RelayCommand(JoinGame);
            LoadSessionData();
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

            if (!IsGuest && !friendsLoaded)
            {
                _ = LoadFriendsAsync(); //Por que el _ ?
            }
            //lógica para cargar la lista de amigos
            // LoadFriendsListAsync();
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
                friendsLoaded = true;
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

        private void AddFriend(object parameter)
        {
            // Lógica para abrir la ventana de añadir amigo
            // windowService.ShowAddFriendWindow();
        }

        private bool CanDeleteFriend()
        {
            return SelectedFriend != null && !IsGuest;
        }
        private async Task DeleteFriendAsync()
        {
            MessageBoxResult result = MessageBox.Show(
            string.Format("¿Estás seguro de que quieres eliminar a {0} de tu lista de amigos?", SelectedFriend.Nickname),
            "Confirmar Eliminación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                return; // El usuario canceló
            }

            try
            {
                // 2. Llamar al servidor
                bool success = await friendsManagerService.DeleteFriendAsync(
                    SessionManager.CurrentUser.PlayerId,
                    SelectedFriend.PlayerId
                );

                // 3. Procesar la respuesta
                if (success)
                {
                    windowService.ShowPopUp("Éxito", "Amigo eliminado correctamente.", PopUpIcon.Success);
                    // Actualizamos la lista en la UI al instante, sin volver a llamar al servidor
                    Friends.Remove(SelectedFriend);
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
            var settingsViewModel = new SettingsViewModel(App.WindowService);
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

