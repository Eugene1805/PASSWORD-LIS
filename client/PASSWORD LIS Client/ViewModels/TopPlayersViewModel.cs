using PASSWORD_LIS_Client.TopPlayersManagerServiceReference;
using System.Collections.ObjectModel;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class TopPlayersViewModel : BaseViewModel
    {
        private const int NumberOfTeams = 5;

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

        public TopPlayersViewModel()
        {
            TopTeams = new ObservableCollection<TeamDTO>();
            LoadTopPlayersAsync();
        }

        private async Task LoadTopPlayersAsync()
        {
            IsLoading = true;
            var client = new TopPlayersManagerClient();
            bool success = false;

            try
            {
                var teamsTopArray = await client.GetTopAsync(NumberOfTeams);
                TopTeams = new ObservableCollection<TeamDTO>(teamsTopArray);
                success = true;
            }
            catch (FaultException<ServiceErrorDetailDTO> ex)
            {
                MessageBox.Show(ex.Detail.Message, "Error del Servidor", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show("No se pudo conectar con el servidor. Verifica tu conexión o que el servidor esté en línea.", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show($"Ocurrió un error de comunicación: {ex.Message}", "Error de Comunicación", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (System.Exception ex) 
            {
                MessageBox.Show($"Se produjo un error inesperado: {ex.Message}", "Error Inesperado", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (success)
                {
                    client.Close();
                }
                else
                {
                    client.Abort();
                }
                IsLoading = false;
            }
        }
    }
}
