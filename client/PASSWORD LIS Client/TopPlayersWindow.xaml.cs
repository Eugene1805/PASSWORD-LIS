using PASSWORD_LIS_Client.TopPlayersManagerServiceReference;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.Windows;
using System.Windows.Data;
using ServiceErrorDetailDTO = PASSWORD_LIS_Client.TopPlayersManagerServiceReference.ServiceErrorDetailDTO;

namespace PASSWORD_LIS_Client
{
    /// <summary>
    /// Lógica de interacción para TopPlayersWindow.xaml
    /// </summary>
    public partial class TopPlayersWindow : Window
    {
        private readonly int numberOfTeams = 10;
        public TopPlayersWindow()
        {
            InitializeComponent();
        }
        private async void TopPlayersWindowLoaded(object sender, RoutedEventArgs e)
        {
            var client = new TopPlayersManagerClient();
            bool success = false;
            try
            {
                
                var teamsTop = await client.GetTopAsync(numberOfTeams);
                topTeamsDataGrid.ItemsSource = teamsTop;
                success = true;
            }
            catch (FaultException<ServiceErrorDetailDTO> ex)
            {
                MessageBox.Show(ex.Detail.Message, "Error del Servidor");
            }
            catch (EndpointNotFoundException)
            {
                MessageBox.Show("No se pudo conectar con el servidor. Verifica tu conexión o que el servidor esté en línea.");
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show($"Ocurrió un error de comunicación: {ex.Message}");
            }
            catch (Exception ex) // Captura cualquier otro error inesperado
            {
                MessageBox.Show($"Se produjo un error inesperado: {ex.Message}");
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
                topTeamsDataGrid.Visibility = Visibility.Visible;
            }

        }

    }

    public class NicknamesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // We use IEnumerable<string> because it works with List<T> and arrays T[]
            if (value is IEnumerable<string> nombres)
            {
                // Si la colección está vacía, devuelve un texto indicativo
                if (!nombres.Any())
                {
                    return "(Sin jugadores)";
                }
                return string.Join(" & ", nombres);
            }
            return string.Empty; 
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
