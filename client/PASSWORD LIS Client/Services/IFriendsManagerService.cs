using PASSWORD_LIS_Client.FriendsManagerServiceReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.Services
{
    public interface IFriendsManagerService
    {
        Task<FriendDTO[]> GetFriendsAsync(int userAccountId);

        Task<bool> DeleteFriendAsync(int currentUserId, int friendToDeleteId);
    }

    public class WcfFriendsManagerService : IFriendsManagerService
    {
        public async Task<FriendDTO[]> GetFriendsAsync(int userAccountId)
        {
            var wcfClient = new FriendsManagerClient();
            try
            {
                var result = await wcfClient.GetFriendsAsync(userAccountId);
                wcfClient.Close();
                return result;
            }
            catch
            {
                wcfClient.Abort();
                throw;
            }
        }

        public async Task<bool> DeleteFriendAsync(int currentUserId, int friendToDeleteId)
        {
            var wcfClient = new FriendsManagerClient();
            try
            {
                var result = await wcfClient.DeleteFriendAsync(currentUserId, friendToDeleteId);
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
