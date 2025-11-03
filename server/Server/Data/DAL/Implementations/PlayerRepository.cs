using Data.DAL.Interfaces;
using Data.Model;
using Data.Util;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace Data.DAL.Implementations
{
    public class PlayerRepository : IPlayerRepository
    {
        public async Task<Player> GetPlayerByEmailAsync(string email)
        {
            using(var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                var userAccount = context.UserAccount.FirstOrDefault(u =>
                u.Email.Equals(email));
                if (userAccount == null)
                {
                    return new Player { Id = -1 };
                }
                return await context.Player
                           .Include("UserAccount")
                           .FirstOrDefaultAsync(p => p.UserAccountId == userAccount.Id);
            }
        }

        public async Task<Player> GetPlayerByIdAsync(int playerId)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                var userAccount = context.UserAccount.FirstOrDefault(u =>
                u.Email.Equals(playerId));
                if (userAccount == null)
                {
                    return new Player { Id = -1 };
                }
                return await context.Player
                        .Include("UserAccount")
                        .FirstOrDefaultAsync(p => p.Id == playerId);
            }
        }
    }
}
