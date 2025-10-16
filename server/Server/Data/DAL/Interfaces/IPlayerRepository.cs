using Data.Model;

namespace Data.DAL.Interfaces
{
    public interface IPlayerRepository
    {
        Player GetPlayerByUsername(string username);
    }
}
