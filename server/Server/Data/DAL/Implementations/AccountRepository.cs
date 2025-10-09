using Data.DAL.Interfaces;
using Data.Model;
using Data.Util;
using System;
using System.Data.Entity;
using System.Linq;

namespace Data.DAL.Implementations
{
    public class AccountRepository : IAccountRepository
    {
        public bool CreateAccount(UserAccount account)
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
                    Console.WriteLine(ex.ToString());
                    return false;
                }
            }
           
        }

        public bool AccountAlreadyExist(string email)
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

        public UserAccount GetUserByEmail(string email)
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

        public bool VerifyEmail(string email)
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
                    Console.WriteLine(ex.Message);
                    return false;
                }
            }
        }

        public bool ResetPassword(string email, string password)
        {
            using(var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                try
                {
                    UserAccount user = GetUserByEmail(email);
                    user.PasswordHash = password;
                    context.UserAccount.Attach(user);
                    context.SaveChanges ();
                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
        }

        public bool UpdateUserAvatar(int userId, int newPhotoId)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                try
                {
                    var player = context.Player.Include(p => p.UserAccount)
                        .FirstOrDefault(p => p.Id == userId);
                    if (player == null || player.UserAccount == null)
                    {
                        return false;
                    }

                    player.UserAccount.PhotoId = (byte?)newPhotoId;
                    context.SaveChanges();
                    return true;

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return false;
                }
            }
        }
    }
}
