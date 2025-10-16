using PASSWORD_LIS_Client.TopPlayersManagerServiceReference;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.Services
{
    public interface ITopPlayersManagerService
    {
        Task<TeamDTO[]> GetTopAsync(int numberOfTeams);
    }

    public class WcfTopPlayersManagerService : ITopPlayersManagerService
    {
        public async Task<TeamDTO[]> GetTopAsync(int numberOfTeams)
        {
            var wcfClient = new TopPlayersManagerClient();
            try
            {
                TeamDTO[] result = await wcfClient.GetTopAsync(numberOfTeams);
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
