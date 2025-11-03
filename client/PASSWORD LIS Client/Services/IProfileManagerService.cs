using PASSWORD_LIS_Client.ProfileManagerServiceReference;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.Services
{
    public interface IProfileManagerService
    {
        Task<UserDTO> UpdateProfileAsync(UserDTO profileDTO);
    }

    public class WcfProfileManagerService : IProfileManagerService
    {
        public async Task<UserDTO> UpdateProfileAsync(UserDTO profileDTO)
        {
            var wcfClient = new ProfileManagerClient();
            try
            {
                UserDTO result = await wcfClient.UpdateProfileAsync(profileDTO);
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
