using System;

namespace PASSWORD_LIS_Client.Utils
{
    public static class Messenger
    {
        public static event Action UserLoggedOut;
        public static void SendUserLoggedOut()
        {
            UserLoggedOut?.Invoke();
        }

        public static void Unsubscribe(Action handler)
        {
            UserLoggedOut -= handler;
        }
    }
}
