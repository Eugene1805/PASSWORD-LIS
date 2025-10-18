using Data.DAL.Interfaces;
using Data.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Data.Entity;
using Data.Util;

namespace Data.DAL.Implementations
{
    public class FriendshipRepository : IFriendshipRepository
    {
        
        public List<UserAccount> GetFriendsByUserAccountId(int userAccountId)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                var player = context.Player.FirstOrDefault(p => p.UserAccountId == userAccountId);
                if (player == null)
                {
                    return new List<UserAccount>();
                }

                int playerId = player.Id;

                var friendIdsFromRequesters = context.Friendship
                    .Where(f => f.AddresseeId == playerId && f.Status == 1)
                    .Select(f => f.RequesterId);

                var friendIdsFromAddressees = context.Friendship
                    .Where(f => f.RequesterId == playerId && f.Status == 1)
                    .Select(f => f.AddresseeId);

                var allFriendIds = friendIdsFromRequesters.Concat(friendIdsFromAddressees).ToList();

                var friends = context.UserAccount
                    .Where(u => u.Player.Any(p => allFriendIds.Contains(p.Id)))
                    .Include(u => u.Player) 
                    .ToList();

                return friends;
            }
        }

        public bool DeleteFriendship(int currentUserId, int friendToDeleteId)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                try
                {
                    var friendship = context.Friendship.FirstOrDefault(f =>
                        (f.RequesterId == currentUserId && f.AddresseeId == friendToDeleteId) ||
                        (f.RequesterId == friendToDeleteId && f.AddresseeId == currentUserId));

                    if (friendship != null)
                    {
                        context.Friendship.Remove(friendship);
                        context.SaveChanges();
                        return true; // Éxito
                    }
                    return false; // No se encontró la amistad
                }
                catch (Exception)
                {
                    return false; // Error en la base de datos
                }
            }
        }
    }
}
