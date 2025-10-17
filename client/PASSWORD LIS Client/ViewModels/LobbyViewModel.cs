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

        //? POR QUE LA PROPIEDAD DE NICKNAME

        private bool isGuest;
        public bool IsGuest
        {
            get => isGuest; 
            set { isGuest = value; OnPropertyChanged(); }
        }

        //Propiedad para la lista de amigos

        public ICommand NavigateToProfileCommand { get; }
        public ICommand AddFriendCommand { get; }
        public ICommand DeleteFriendCommand { get; }
        public ICommand ShowTopPlayersCommand { get; }
        public ICommand HowToPlayCommand { get; }
        public ICommand SettingsCommand { get; }

        private readonly IWindowService windowService;

        public LobbyViewModel(IWindowService windowService)
        {
            this.windowService = windowService;

            NavigateToProfileCommand = new RelayCommand(NavigateToProfile, (_) => !IsGuest); // Solo se puede ejecutar si NO es invitado
            AddFriendCommand = new RelayCommand(AddFriend);
            DeleteFriendCommand = new RelayCommand(DeleteFriend);
            ShowTopPlayersCommand = new RelayCommand(ShowTopPlayers);

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
            //Nickname = currentUser.Nickname; Por qué la propiedad de nickname?
            IsGuest = currentUser.PlayerId < 0;

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
    }
}

