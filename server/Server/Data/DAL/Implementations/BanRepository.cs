using Data.DAL.Interfaces;
using Data.Model;
using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace Data.DAL.Implementations
{
    public class BanRepository : IBanRepository
    {
        private readonly IDbContextFactory contextFactory;
        public BanRepository(IDbContextFactory ContextFactory)
        {
            this.contextFactory = ContextFactory;
        }
        public async Task AddBanAsync(Ban newBan)
        {
            using (var context = contextFactory.CreateDbContext())
            {
                context.Ban.Add(newBan);
                await context.SaveChangesAsync();
            }
        }

        public async Task<Ban> GetActiveBanForPlayerAsync(int playerId)
        {
            using (var context = contextFactory.CreateDbContext())
            {
                var now = DateTime.UtcNow;
                return await context.Ban
                    .Where(b => b.PlayerId == playerId && b.EndTime > now)
                    .FirstOrDefaultAsync();
            }
        }

        public async Task<DateTime?> GetLastBanEndTimeAsync(int playerId)
        {
            using (var context = contextFactory.CreateDbContext())
            {
                var lastBan = await context.Ban
                    .Where(b => b.PlayerId == playerId)
                    .OrderByDescending(b => b.EndTime)
                    .FirstOrDefaultAsync();
                return lastBan?.EndTime;
            }
        }
    }
}
