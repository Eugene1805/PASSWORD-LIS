using Data.DAL.Interfaces;
using Data.Model;
using Data.Util;
using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace Data.DAL.Implementations
{
    public class BanRepository : IBanRepository
    {
        public async Task AddBanAsync(Ban newBan)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                context.Ban.Add(newBan);
                await context.SaveChangesAsync();
            }
        }

        public async Task<Ban> GetActiveBanForPlayerAsync(int playerId)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                var now = DateTime.UtcNow;
                return await context.Ban
                    .Where(b => b.PlayerId == playerId && b.EndTime > now)
                    .FirstOrDefaultAsync();
            }
        }
    }
}
