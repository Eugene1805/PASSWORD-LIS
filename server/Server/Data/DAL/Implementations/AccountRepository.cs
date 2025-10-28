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
                UserAccount userAccount = context.UserAccount
                    .Include(u => u.Player)
                    .Include(u => u.SocialAccount)
                    .FirstOrDefault(u => u.Email == email);
                return userAccount;
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
        public bool UpdateUserProfile(int playerId, UserAccount updatedAccountData, List<SocialAccount> updatedSocialsAccounts)
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

                        var userAccountToUpdate = player.UserAccount;

                        userAccountToUpdate.FirstName = updatedAccountData.FirstName;
                        userAccountToUpdate.LastName = updatedAccountData.LastName;
                        userAccountToUpdate.PhotoId = updatedAccountData.PhotoId;

                        // TO DO PASAR LO DE ELIMINAR Y AGREGAR LAS REDES A OTRO METODO
                        var socialsToDelete = userAccountToUpdate.SocialAccount
                            .Where(s => !updatedSocialsAccounts.Any(us => us.Provider == s.Provider)).ToList();
                        foreach (var social in socialsToDelete)
                        {
                            context.SocialAccount.Remove(social);
                        }

                        foreach (var updatedSocial in updatedSocialsAccounts)
                        {
                            var existingSocial = userAccountToUpdate.SocialAccount
                                .FirstOrDefault(s => s.Provider == updatedSocial.Provider);

                            if (existingSocial != null)
                            {
                                existingSocial.Username = updatedSocial.Username; 
                            }
                            else
                            {
                                userAccountToUpdate.SocialAccount.Add(updatedSocial);
                            }
                        }

                        context.SaveChanges();
                        transaction.Commit();
                        return true;
                    }
                    catch (System.Exception)
                    {
                        transaction.Rollback();
                        return false;
                    }
                }
            }
        }

        public UserAccount GetUserByPlayerId(int playerId)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                return context.UserAccount
                    .Include(u => u.Player)
                    .FirstOrDefault(u => u.Player.Any(p => p.Id == playerId));
            }
        }
        public UserAccount GetUserByUserAccountId(int userAccountId)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                return context.UserAccount
                              .Include(u => u.Player)
                              .FirstOrDefault(u => u.Id == userAccountId);
            }
        }
    }
}
