using PASSWORD_LIS_Client.LoginManagerServiceReference;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.Services
{
    public interface ILoginManagerService
    {
        Task<UserDTO> LoginAsync(string email, string password);

    }

    public class WcfLoginManagerService : ILoginManagerService
    {
        public async Task<UserDTO> LoginAsync(string email, string password)
        {
            var wcfClient = new LoginManagerClient();
            try
            {
                UserDTO result = await wcfClient.LoginAsync(email, password);
                wcfClient.Close();
                return result;
            }
            catch
            {
                wcfClient.Abort();
                throw;
            }
        }
    }
}
