using PASSWORD_LIS_Client.TopPlayersManagerServiceReference;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
            LoadTop();
        }
        private void LoadTop()
        {
            try
            {
                var client = new TopPlayersManagerClient();
                List<TeamDTO> teamsTop = client.GetTop(numberOfTeams).ToList();

                topTeamsDataGrid.ItemsSource = teamsTop;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el top de equipos: {ex.Message}");
            }
        }

    }

    public class NicknamesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Usando IEnumerable<string> funciona para List<T> y para arrays T[]
            if (value is IEnumerable<string> nombres)
            {
                // Si la colección está vacía, devuelve un texto indicativo
                if (!nombres.Any())
                {
                    return "(Sin jugadores)";
                }
                return string.Join(" & ", nombres);
            }
            return string.Empty; // Devuelve vacío si el valor no es una colección de strings
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
