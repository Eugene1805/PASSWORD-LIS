using Data.DAL.Interfaces;
using Data.Exceptions;
using Data.Model;
using Data.Util;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace Data.DAL.Implementations
{
    public class AccountRepository : IAccountRepository
    {
        public async Task CreateAccountAsync(UserAccount account)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                if ( await context.UserAccount.AnyAsync(a => a.Email == account.Email))
                {
                    throw new DuplicateAccountException($"Una cuenta con el email '{account.Email}' ya existe.");
                }
                    context.UserAccount.Add(account);
                    context.Player.Add(new Player { UserAccount = account });
                    await context.SaveChangesAsync();
                
            }

        }

        public bool AccountAlreadyExist(string email)
        {
            if (String.IsNullOrEmpty(email))
            {
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
                    UserAccount userAccount = context.UserAccount
                        .Include(u => u.Player)
                        .Include(u => u.SocialAccount)
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
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                try
                {
                    UserAccount user = context.UserAccount.FirstOrDefault(u => u.Email == email);

                    if (user == null)
                    {
                        return false;
                    }
                    user.EmailVerified = true;
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

        public bool ResetPassword(string email, string passwordHash)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                try
                {
                    UserAccount user = context.UserAccount.FirstOrDefault(u => u.Email == email);

                    if (user == null)
                    {
                        return false;
                    }

                    user.PasswordHash = passwordHash;
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

        public bool UpdateUserProfile(int playerId, string nickname, string firstName, string lastName, int photoId, Dictionary<string, string> socialAccounts)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                using (var transaction = context.Database.BeginTransaction())
                {
                    try
                    {
                        var player = context.Player
                            .Include(p => p.UserAccount.SocialAccount)
                            .FirstOrDefault(p => p.Id == playerId);

                        if (player == null || player.UserAccount == null)
                        {
                            return false;
                        }

                        var userAccount = player.UserAccount;

                        userAccount.Nickname = nickname;
                        userAccount.FirstName = firstName;
                        userAccount.LastName = lastName;

                        if (photoId >= 1 && photoId <= 6)
                        {
                            userAccount.PhotoId = (byte?)photoId;
                        }
                        else
                        {
                            userAccount.PhotoId = null;
                        }

                            foreach (var socialAcccount in socialAccounts)
                            {
                                var existingAccount = userAccount.SocialAccount
                                    .FirstOrDefault(sa => sa.Provider == socialAcccount.Key);

                                if (existingAccount != null)
                                {
                                    if (string.IsNullOrEmpty(socialAcccount.Value))
                                    {
                                        context.SocialAccount.Remove(existingAccount);
                                    }
                                    else
                                    {
                                        existingAccount.Username = socialAcccount.Value;
                                    }
                                }
                                else if (!string.IsNullOrEmpty(socialAcccount.Value))
                                {
                                    userAccount.SocialAccount.Add(new SocialAccount
                                    {
                                        Provider = socialAcccount.Key,
                                        Username = socialAcccount.Value
                                    });
                                }
                            }

                        context.SaveChanges();
                        transaction.Commit();

                        return true;
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        return false;
                    }
                }
            }
        }

    }
}
