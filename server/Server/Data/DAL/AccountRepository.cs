using Data.Model;
using Data.Util;
using System;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Linq;

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
            if (String.IsNullOrEmpty(email)){
                return false;
            }
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

        public static bool VerifyEmail(string email)
        {
            using(var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                try
                {
                    UserAccount user = GetUserByEmail (email);
                    user.EmailVerified = true;
                    context.UserAccount.Attach(user);
                    context.SaveChanges();
                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
        }
    }
}
