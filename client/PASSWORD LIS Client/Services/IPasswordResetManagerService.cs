using PASSWORD_LIS_Client.PasswordResetManagerServiceReference;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.Services
{
    public interface IPasswordResetManagerService
    {
        Task<bool> ValidatePasswordResetCodeAsync(EmailVerificationDTO dto);
        Task<bool> RequestPasswordResetCodeAsync(EmailVerificationDTO dto);
        Task<bool> ResetPasswordAsync(PasswordResetDTO dto);
    }

    public class WcfPasswordResetManagerService : IPasswordResetManagerService
    {
        public async Task<bool> ValidatePasswordResetCodeAsync(EmailVerificationDTO dto)
        {
            var wcfClient = new PasswordResetManagerClient();
            try
            {
                bool result = await wcfClient.ValidatePasswordResetCodeAsync(dto);
                wcfClient.Close();
                return result;
            }
            catch
            {
                wcfClient.Abort();
                throw;
            }
        }
        public async Task<bool> RequestPasswordResetCodeAsync(EmailVerificationDTO dto)
        {
            var wcfClient = new PasswordResetManagerClient();
            try
            {
                bool result = await wcfClient.RequestPasswordResetCodeAsync(dto);
                wcfClient.Close();
                return result;
            }
            catch
            {
                wcfClient.Abort();
                throw;
            }
        }
        public async Task<bool> ResetPasswordAsync(PasswordResetDTO dto)
        {
            var wcfClient = new PasswordResetManagerClient();
            try
            {
                bool result = await wcfClient.ResetPasswordAsync(dto);
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
