using System;

namespace PASSWORD_LIS_Client.Utils
{
    public static class AvatarHelper
    {

        public static Uri GetAvatarUriById(int photoId)
        {
            string fileName;
            if (photoId >= 1 && photoId <= 6)
            {
                fileName = $"Avatar{photoId}.png";
            }
            else
            {
                fileName = "AvatarDefault.png";
            }

            string resourcePath = $"/Resources/{fileName}";

            return new Uri($"pack://application:,,,{resourcePath}", UriKind.Absolute);
        }
    }
}
