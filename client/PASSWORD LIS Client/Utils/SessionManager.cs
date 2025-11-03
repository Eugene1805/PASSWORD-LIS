using UserDTO = PASSWORD_LIS_Client.LoginManagerServiceReference.UserDTO;

namespace PASSWORD_LIS_Client.Utils
{
    public static class SessionManager
    {
        public static UserDTO CurrentUser { get; private set; }

        public static void Login(UserDTO user)
        {
            CurrentUser = user;
        }

        public static void Logout()
        {
            CurrentUser = null;
        }

        public static bool IsUserLoggedIn()
        {
            return CurrentUser != null;
        }

    }
}
