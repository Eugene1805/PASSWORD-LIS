using Data.DAL.Interfaces;
using Data.Model;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace Data.DAL.Implementations
{
    public class PlayerRepository : IPlayerRepository
    {
        private readonly IDbContextFactory contextFactory;
        public PlayerRepository(IDbContextFactory contextFactory)
        {
            this.contextFactory = contextFactory;
        }
        public async Task<Player> GetPlayerByEmailAsync(string email)
        {
            using(var context = contextFactory.CreateDbContext())
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
            using (var context = contextFactory.CreateDbContext())
            {
                var player = await context.Player
                    .Include(p => p.UserAccount)
                    .FirstOrDefaultAsync(p => p.Id == playerId);
                return player ?? new Player { Id = -1 };
            }
        }

        public async Task UpdatePlayerTotalPointsAsync(int playerId, int pointsGained)
        {
            using (var context = contextFactory.CreateDbContext())
            {
                var player = await context.Player.FindAsync(playerId);
                if (player != null)
                {
                    player.TotalPoints += pointsGained;
                    await context.SaveChangesAsync();
                }
            }
        }
    }
}
