using Data.Model;
using System.Threading.Tasks;

namespace Data.DAL.Interfaces
{
    public interface IPlayerRepository
    {
        Task<Player> GetPlayerByEmailAsync(string email);
        Task<Player> GetPlayerByIdAsync(int playerId);
        Task UpdatePlayerTotalPointsAsync(int playerId, int pointsGained);
    }
}
