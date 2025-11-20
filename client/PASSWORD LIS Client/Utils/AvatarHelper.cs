using System;

namespace PASSWORD_LIS_Client.Utils
{
    public static class AvatarHelper
    {
        public static Uri GetAvatarUriById(int photoId)
        {
            int normalizedId = (photoId >= 1 && photoId <= 6) ? photoId : 0;
            string fileName = normalizedId == 0 ? "AvatarDefault.png" : $"Avatar{normalizedId}.png";

            string relativePath = $"Resources/{fileName}";

            var baseUri = new Uri("pack://application:,,,/", UriKind.Absolute);
            return new Uri(baseUri, relativePath); // Absolute pack URI result
        }
    }
}
