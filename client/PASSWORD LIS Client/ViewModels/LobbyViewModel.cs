using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using PASSWORD_LIS_Client.FriendsManagerServiceReference;

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
        public ICommand NavigateToProfileCommand { get; }
        public ICommand AddFriendCommand { get; }
        public ICommand DeleteFriendCommand { get; }
        public ICommand ShowTopPlayersCommand { get; }
        public ICommand HowToPlayCommand { get; }
        public ICommand SettingsCommand { get; }

        private readonly IWindowService windowService;
        private readonly IFriendsManagerService friendsManagerService;
        public LobbyViewModel(IWindowService windowService, IFriendsManagerService friendsManagerService)
        {
            this.windowService = windowService;
            this.friendsManagerService = friendsManagerService;

            NavigateToProfileCommand = new RelayCommand(NavigateToProfile, (_) => !IsGuest); // Solo se puede ejecutar si NO es invitado
            Friends = new ObservableCollection<FriendDTO>();
            AddFriendCommand = new RelayCommand(AddFriend);
            DeleteFriendCommand = new RelayCommand(DeleteFriend);
            ShowTopPlayersCommand = new RelayCommand(ShowTopPlayers);
            HowToPlayCommand = new RelayCommand(ShowHowToPlay);
            SettingsCommand = new RelayCommand(ShowSettings);

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

            if (!IsGuest)
            {
                _ = LoadFriendsAsync(); //Por que el _ ?
            }
            //lógica para cargar la lista de amigos
            // LoadFriendsListAsync();
        }

        private void NavigateToProfile(object parameter)
        {
            var profileViewModel = new ProfileViewModel(new WcfProfileManagerService(), new WindowService());


            var page = parameter as Page;
            if (page != null)
            {
                page.NavigationService.Navigate(new ProfilePage { DataContext = profileViewModel });
            }
        }

        private async Task LoadFriendsAsync()
        {
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

        private void AddFriend(object parameter)
        {
            // Lógica para abrir la ventana de añadir amigo
            // windowService.ShowAddFriendWindow();
        }

        private void DeleteFriend(object parameter)
        {
            // Lógica para eliminar el amigo seleccionado de la lista
        }

        private void ShowTopPlayers(object parameter)
        {
            var topPlayersViewModel = new TopPlayersViewModel(new WcfTopPlayersManagerService(), new WindowService());
            var topPlayersWindow = new Views.TopPlayersWindow { DataContext = topPlayersViewModel};
            topPlayersWindow.ShowDialog();
        }
        private void ShowHowToPlay(object parameter)
        {/*
            var howToPlayViewModel = new HowToPlayViewModel();
            var howToPlayWindow = new HowToPlayWindow { DataContext = howToPlayViewModel };

            howToPlayWindow.ShowDialog();*/
            Console.WriteLine($"ShowHowToPlay ejecutado");
            Console.WriteLine($"Parameter type: {parameter?.GetType().Name}");
            Console.WriteLine($"Parameter is Page: {parameter is Page}");

            var page = parameter as Page;
            if (page != null)
            {
                Console.WriteLine($"NavigationService is null: {page.NavigationService == null}");
                var waitingRoomViewModel = new WaitingRoomViewModel(new WcfWaitingRoomManagerService(), windowService);
                var waitingRoomPage = new WaitingRoomPage { DataContext = waitingRoomViewModel };

                page.NavigationService.Navigate(waitingRoomPage);
                Console.WriteLine("Navigate method called");
            }
            else
            {
                Console.WriteLine("Parameter is not a Page, using fallback");
            }
        }

        private void ShowSettings(object parameter)
        {
            var settingsViewModel = new SettingsViewModel(new WindowService());
            var settingsWindow = new SettingsWindow { DataContext = settingsViewModel };
            settingsWindow.ShowDialog();

            if (settingsViewModel.WasLogoutSuccessful)
            {
                windowService.ShowLoginWindow();
                windowService.CloseMainWindow();
            }
        }
    }
}

