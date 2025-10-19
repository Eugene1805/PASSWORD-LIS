using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.TopPlayersManagerServiceReference;
using PASSWORD_LIS_Client.Utils;
using PASSWORD_LIS_Client.Views;
using System;
using System.Collections.ObjectModel;
using System.ServiceModel;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class TopPlayersViewModel : BaseViewModel
    {
        private const int NumberOfTeams = 5;
        private readonly ITopPlayersManagerService playersManagerClient;
        private readonly IWindowService windowService;
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
                var teamsTopArray = await playersManagerClient.GetTopAsync(NumberOfTeams);
                TopTeams = new ObservableCollection<TeamDTO>(teamsTopArray);
            }
            catch (FaultException<ServiceErrorDetailDTO>)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.errorTitleText,
                            Properties.Langs.Lang.unexpectedServerErrorText, PopUpIcon.Error);
            }
            catch (EndpointNotFoundException)
            {
                this.windowService.ShowPopUp(Properties.Langs.Lang.connectionErrorTitleText,
                            Properties.Langs.Lang.serverConnectionInternetErrorText, PopUpIcon.Warning);
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
                IsLoading = false;
            }
        }
    }
}
