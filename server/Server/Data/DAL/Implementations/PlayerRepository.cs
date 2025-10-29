using Data.DAL.Interfaces;
using Data.Model;
using Data.Util;
using System.Linq;
using System.Threading.Tasks;

namespace Data.DAL.Implementations
{
    public class PlayerRepository : IPlayerRepository
    {
        public Player GetPlayerByEmail(string email)
        {
            using(var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                var userAccount = context.UserAccount.FirstOrDefault(u =>
                u.Email.Equals(email));
                if (userAccount == null)
                {
                    return null;
                }
                return context.Player
                           .Include("UserAccount")
                           .FirstOrDefault(p => p.UserAccountId == userAccount.Id);
            }
        }

        public async Task<Player> GetPlayerByIdAsync(int playerId)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                return await Task.Run(() =>
                    context.Player
                        .Include("UserAccount")
                        .FirstOrDefault(p => p.Id == playerId)
                );
            }
        }
    }
}
