using PASSWORD_LIS_Client.AccountManagerServiceReference;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.Services
{
    public interface IAccountManagerService
    {
        Task CreateAccountAsync(NewAccountDTO userAccount);
        Task<bool> IsNicknameInUseAsync(string nickname);
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

        public async Task<bool> IsNicknameInUseAsync(string nickname)
        {
            var wcfClient = new AccountManagerClient();
            try
            {
                bool isInUse = await wcfClient.IsNicknameInUseAsync(nickname);
                wcfClient.Close(); 
                return isInUse;
            }
            catch
            {
                wcfClient.Abort();
                throw; 
            }
        }
    }
}
