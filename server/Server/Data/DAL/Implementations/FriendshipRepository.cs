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
                        return true;
                    }
                    return false; 
                }
                catch (Exception)
                {
                    return false; 
                }
            }
        }

        public bool CreateFriendRequest(int requesterPlayerId, int addresseePlayerId)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                try
                {
                    bool requestExists = context.Friendship.Any(f =>
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
                        RequestedAt = System.DateTime.UtcNow
                    };

                    context.Friendship.Add(newRequest);
                    context.SaveChanges();
                    return true;
                }
                catch (Exception)
                {
                    return false; 
                }
            }
        }

        public List<Friendship> GetPendingRequests(int userAccountId)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                var player = context.Player.FirstOrDefault(p => p.UserAccountId == userAccountId);
                if (player == null)
                {
                    return new List<Friendship>();
                }
                // Buscamos todas las solicitudes donde YO soy el destinatario y el estado es Pendiente
                return context.Friendship
                    .Include(f => f.Player1.UserAccount)
                    .Where(f => f.AddresseeId == player.Id && f.Status == 0)
                    .ToList();
            }
        }

        public bool RespondToFriendRequest(int requesterPlayerId, int addresseePlayerId, bool accept)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                var request = context.Friendship.FirstOrDefault(f =>
                    f.RequesterId == requesterPlayerId && f.AddresseeId == addresseePlayerId && f.Status == 0);

                if (request == null)
                {
                    return false;
                }
                if (accept)
                {
                    request.Status = 1; 
                    request.RespondedAt = System.DateTime.UtcNow;
                }
                else
                {
                    context.Friendship.Remove(request); 
                }

                context.SaveChanges();
                return true;
            }
        }
    }
}
