using Data.Model;
using System.Threading.Tasks;

namespace Data.DAL.Interfaces
{
    public interface IBanRepository
    {
        Task AddBanAsync(Ban newBan);
        Task<Ban> GetActiveBanForPlayerAsync(int playerId);
    }
}
