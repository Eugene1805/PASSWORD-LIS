using PASSWORD_LIS_Client.VerificationCodeManagerServiceReference;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.Services
{
    public interface IVerificationCodeManagerService
    {
        Task<bool> VerifyEmailAsync(EmailVerificationDTO dto);
        Task<bool> ResendVerificationCodeAsync(string email);
    }

    public class WcfVerificationCodeManagerService : IVerificationCodeManagerService
    {
        public async Task<bool> VerifyEmailAsync(EmailVerificationDTO dto)
        {
            var wcfClient = new AccountVerificationManagerClient();
            try
            {
                bool result = await wcfClient.VerifyEmailAsync(dto);
                wcfClient.Close();
                return result;
            }
            catch
            {
                wcfClient.Abort();
                throw;
            }
        }
        public async Task<bool> ResendVerificationCodeAsync(string email)
        {
            var wcfClient = new AccountVerificationManagerClient();
            try
            {
                bool result = await wcfClient.ResendVerificationCodeAsync(email);
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
