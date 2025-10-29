using Data.Model;
using System.Threading.Tasks;

namespace Data.DAL.Interfaces
{
    public interface IPlayerRepository
    {
        Player GetPlayerByEmail(string email);
        Task<Player> GetPlayerByIdAsync(int playerId);
    }
}
