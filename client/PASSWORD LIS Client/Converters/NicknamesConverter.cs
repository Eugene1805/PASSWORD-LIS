using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace PASSWORD_LIS_Client.Converters
{
    public class NicknamesConverter : IValueConverter
    {
        private const string ConcatenationSymbol = " & ";
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable<string> nombres)
            {
                if (!nombres.Any())
                {
                    return string.Empty;
                }
                return string.Join(ConcatenationSymbol, nombres);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
