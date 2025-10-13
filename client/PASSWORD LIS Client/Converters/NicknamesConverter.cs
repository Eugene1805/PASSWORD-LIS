using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace PASSWORD_LIS_Client.Converters
{
    public class NicknamesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Usamos IEnumerable<string> porque funciona con List<T> y arrays T[]
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
