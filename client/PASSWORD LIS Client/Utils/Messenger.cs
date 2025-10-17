using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
