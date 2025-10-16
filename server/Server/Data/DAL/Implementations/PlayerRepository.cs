using Data.DAL.Interfaces;
using Data.Model;
using Data.Util;
using System;
using System.Linq;

namespace Data.DAL.Implementations
{
    public class PlayerRepository : IPlayerRepository
    {
        public Player GetPlayerByUsername(string username)
        {
            using(var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                var userAccount = context.UserAccount.FirstOrDefault(u =>
                u.Nickname.Equals(username, StringComparison.OrdinalIgnoreCase));

                if (userAccount == null)
                {
                    return null;
                }
                return context.Player
                           .Include("UserAccount")
                           .FirstOrDefault(p => p.UserAccountId == userAccount.Id);
            }
        }
    }
}
