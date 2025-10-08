using Data.Model;
using Data.Util;
using System;
using System.Data.Entity;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Data.DAL
{
    public static class AccountRepository
    {
        public static bool CreateAccount(UserAccount account)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                if (AccountAlreadyExist(account.Email))
                {
                    return false;
                }
                try
                {
                    context.UserAccount.Add(account);
                    context.Player.Add(new Player { UserAccount = account });
                    context.SaveChanges();
                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
           
        }

        private static bool AccountAlreadyExist(string email)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                try
                {
                    return context.UserAccount.Any(a => a.Email == email);
                }
                catch (Exception ex)
                {
                    return false;
                }
            }

        }

        public static UserAccount GetUserByEmail(string email)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                try
                {
                    UserAccount userAccount = context.UserAccount.Include(u => u.Player)
                        .FirstOrDefault(u => u.Email == email);
                    return userAccount;
                }
                catch (Exception ex)
                {
                    return null;
                }
            }
        }
    }
}
