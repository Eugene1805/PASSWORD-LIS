using PASSWORD_LIS_Client.AccountManagerServiceReference;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.Services
{
    public interface IAccountManagerService
    {
        Task CreateAccountAsync(NewAccountDTO userAccount);
    }
    public class WcfAccountManagerService : IAccountManagerService
    {
        public async Task CreateAccountAsync(NewAccountDTO userAccount)
        {
            var wcfClient = new AccountManagerClient();
            try
            {
                await wcfClient.CreateAccountAsync(userAccount);
                wcfClient.Close(); 
            }
            catch
            {
                wcfClient.Abort();
                throw; 
            }
        }
    }
}
