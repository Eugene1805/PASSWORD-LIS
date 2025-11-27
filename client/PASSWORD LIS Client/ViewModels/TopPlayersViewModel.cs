using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.TopPlayersManagerServiceReference;
using PASSWORD_LIS_Client.Utils;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class TopPlayersViewModel : BaseViewModel
    {
        private const int NumberOfTeams = 5;
        private readonly ITopPlayersManagerService playersManagerClient;
        private ObservableCollection<TeamDTO> topTeams;
        public ObservableCollection<TeamDTO> TopTeams
        {
            get => topTeams;
            set { topTeams = value; OnPropertyChanged(); }
        }

        private bool isLoading = true;
        public bool IsLoading
        {
            get => isLoading;
            set { isLoading = value; OnPropertyChanged(); }
        }

        public TopPlayersViewModel(ITopPlayersManagerService playersManagerService, IWindowService windowService)
            : base(windowService)
        {
            this.playersManagerClient = playersManagerService;
            this.windowService = windowService;
            TopTeams = new ObservableCollection<TeamDTO>();
            _ = LoadTopPlayersAsync();
        }

        private async Task LoadTopPlayersAsync()
        {
            IsLoading = true;

            try
            {
                await ExecuteAsync(async () =>
                {
                    var teamsTopArray = await playersManagerClient.GetTopAsync(NumberOfTeams);
                    TopTeams = new ObservableCollection<TeamDTO>(teamsTopArray);
                });
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
