using Data.DAL.Interfaces;
using Data.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Data.Entity;
using System.Threading.Tasks;

namespace Data.DAL.Implementations
{
    public class FriendshipRepository : IFriendshipRepository
    {
        private readonly IDbContextFactory contextFactory;
        public FriendshipRepository(IDbContextFactory contextFactory)
        {
            this.contextFactory = contextFactory;
        }
        public async Task<List<UserAccount>> GetFriendsByUserAccountIdAsync(int userAccountId)
        {
            using (var context = contextFactory.CreateDbContext())
            {
                var player = await context.Player.FirstOrDefaultAsync(p => p.UserAccountId == userAccountId);
                if (player == null)
                {
                    return new List<UserAccount>();
                }

                int playerId = player.Id;

                var friendIdsFromRequesters = await context.Friendship
                    .Where(f => f.AddresseeId == playerId && f.Status == 1)
                    .Select(f => f.RequesterId)
                    .ToListAsync();

                var friendIdsFromAddressees = await context.Friendship
                    .Where(f => f.RequesterId == playerId && f.Status == 1)
                    .Select(f => f.AddresseeId)
                    .ToListAsync();

                var allFriendIds = friendIdsFromRequesters.Concat(friendIdsFromAddressees).ToList();

                if (!allFriendIds.Any())
                {
                    return new List<UserAccount>();
                }

                var friends = await context.UserAccount
                    .Where(u => u.Player.Any(p => allFriendIds.Contains(p.Id)))
                    .Include(u => u.Player)
                    .ToListAsync();

                return friends;
            }
        }

        public async Task<bool> DeleteFriendshipAsync(int currentUserId, int friendToDeleteId)
        {
            using (var context = contextFactory.CreateDbContext())
            {
                using (var transaction = context.Database.BeginTransaction())
                {
                    try
                    {
                        var friendship = await context.Friendship.FirstOrDefaultAsync(f =>
                            (f.RequesterId == currentUserId && f.AddresseeId == friendToDeleteId) ||
                            (f.RequesterId == friendToDeleteId && f.AddresseeId == currentUserId));

                        if (friendship != null)
                        {
                            context.Friendship.Remove(friendship);
                            await context.SaveChangesAsync();
                            transaction.Commit();
                            return true;
                        }
                        return false;
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }  
            }
        }

        public async Task<bool> CreateFriendRequestAsync(int requesterPlayerId, int addresseePlayerId)
        {
            using (var context = contextFactory.CreateDbContext())
            {
                using (var transaction = context.Database.BeginTransaction())
                {
                    try
                    {
                        bool requestExists = await context.Friendship.AnyAsync(f =>
                        (f.RequesterId == requesterPlayerId && f.AddresseeId == addresseePlayerId) ||
                        (f.RequesterId == addresseePlayerId && f.AddresseeId == requesterPlayerId));

                        if (requestExists)
                        {
                            return false;
                        }

                        var newRequest = new Friendship
                        {
                            RequesterId = requesterPlayerId,
                            AddresseeId = addresseePlayerId,
                            Status = 0,
                            RequestedAt = DateTime.UtcNow
                        };

                        context.Friendship.Add(newRequest);
                        await context.SaveChangesAsync();
                        transaction.Commit();
                        return true;
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task<List<Friendship>> GetPendingRequestsAsync(int userAccountId)
        {
            using (var context = contextFactory.CreateDbContext())
            {
                var player = await context.Player.FirstOrDefaultAsync(p => p.UserAccountId == userAccountId);
                if (player == null)
                {
                    return new List<Friendship>();
                }

                return await context.Friendship
                    .Include(f => f.Player1.UserAccount)
                    .Where(f => f.AddresseeId == player.Id && f.Status == 0)
                    .ToListAsync();
            }
        }

        public async Task<bool> RespondToFriendRequestAsync(int requesterPlayerId, int addresseePlayerId, bool accept)
        {
            using (var context = contextFactory.CreateDbContext())
            {
                using (var transaction = context.Database.BeginTransaction())
                {
                    try
                    {
                        var request = await context.Friendship.FirstOrDefaultAsync(f =>
                        f.RequesterId == requesterPlayerId && f.AddresseeId == addresseePlayerId && f.Status == 0);

                        if (request == null)
                        {
                            return false;
                        }
                        if (accept)
                        {
                            request.Status = 1;
                            request.RespondedAt = DateTime.UtcNow;
                            context.Entry(request).State = EntityState.Modified;
                        }
                        else
                        {
                            context.Friendship.Remove(request);
                        }

                        await context.SaveChangesAsync();
                        transaction.Commit();
                        return true;
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}
