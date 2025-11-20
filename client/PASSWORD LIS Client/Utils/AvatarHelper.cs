using System;
using System.Configuration;

namespace PASSWORD_LIS_Client.Utils
{
    public static class AvatarHelper
    {
        #pragma warning disable S1075
        private const string DefaultPackSchema = "pack://application:,,,/";
        #pragma warning restore S1075
        public static Uri GetAvatarUriById(int photoId)
        {
            int normalizedId = (photoId >= 1 && photoId <= 6) ? photoId : 0;
            string fileName = normalizedId == 0 ? "AvatarDefault.png" : $"Avatar{normalizedId}.png";

            string relativePath = $"Resources/{fileName}";
            string schema = ConfigurationManager.AppSettings["WPFSchema"];
            if (string.IsNullOrEmpty(schema) || !Uri.IsWellFormedUriString(schema, UriKind.Absolute))
            {
                schema = DefaultPackSchema;
            }
            try
            {
                var baseUri = new Uri(schema, UriKind.Absolute);
                return new Uri(baseUri, relativePath);
            }
            catch (UriFormatException)
            {
                return new Uri(new Uri(DefaultPackSchema), relativePath);
            }
        }
    }
}
