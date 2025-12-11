using PASSWORD_LIS_Client.Utils;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace PASSWORD_LIS_Client.Converters
{
    public class AvatarIdToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int photoId)
            {
                Uri avatarUri = AvatarHelper.GetAvatarUriById(photoId);
                
                if (avatarUri != null)
                {
                    return new BitmapImage(avatarUri);
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
