using Data.DAL.Interfaces;
using Data.Exceptions;
using Data.Model;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace Data.DAL.Implementations
{
    public class AccountRepository : IAccountRepository
    {
        private readonly IDbContextFactory contextFactory;
        public AccountRepository(IDbContextFactory contextFactory)
        {
            this.contextFactory = contextFactory;
        }
        public async Task CreateAccountAsync(UserAccount account)
        {
            var context = contextFactory.CreateDbContext();
            
                if ( await context.UserAccount.AnyAsync(a => a.Email == account.Email))
                {
                    throw new DuplicateAccountException($"An account with the email '{account.Email}' already exists.");
                }
                    context.UserAccount.Add(account);
                    context.Player.Add(new Player { UserAccount = account });
                    await context.SaveChangesAsync();
        }

        public bool AccountAlreadyExist(string email)
        {
            if (String.IsNullOrEmpty(email))
            {
                return false;
            }
            var context = contextFactory.CreateDbContext();
            
                return context.UserAccount.Any(a => a.Email == email);

        }

        public async Task<UserAccount> GetUserByEmailAsync(string email)
        {
            using (var context = contextFactory.CreateDbContext())
            {
                UserAccount userAccount = await context.UserAccount
                    .Include(u => u.Player)
                    .Include(u => u.SocialAccount)
                    .FirstOrDefaultAsync(u => u.Email == email);
                return userAccount;
            }
        }

        public bool VerifyEmail(string email)
        {
            var context = contextFactory.CreateDbContext();
            
                UserAccount user = context.UserAccount.FirstOrDefault(u => u.Email == email);

                if (user == null)
                {
                    return false;
                }
                user.EmailVerified = true;
                context.SaveChanges();
                return true;
            
        }

        public bool ResetPassword(string email, string passwordHash)
        {
            var context = contextFactory.CreateDbContext();
            
                UserAccount user = context.UserAccount.FirstOrDefault(u => u.Email == email);

                if (user == null)
                {
                    return false;
                }

                user.PasswordHash = passwordHash;
                context.SaveChanges();

                return true;
            
        }
        public async Task<bool> UpdateUserProfileAsync(int playerId, UserAccount updatedAccountData,
            List<SocialAccount> updatedSocialsAccounts)
        {
            using (var context = contextFactory.CreateDbContext())
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

                        UpdateSocialAccounts(context, userAccountToUpdate, updatedSocialsAccounts);

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

        public async Task<UserAccount> GetUserByPlayerIdAsync(int playerId)
        {
            using (var context = contextFactory.CreateDbContext())
            {
                return await context.UserAccount
                    .Include(u => u.Player)
                    .FirstOrDefaultAsync(u => u.Player.Any(p => p.Id == playerId));
            }
        }
        public async Task<UserAccount> GetUserByUserAccountIdAsync(int userAccountId)
        {
            using (var context = contextFactory.CreateDbContext())
            {
                return await context.UserAccount
                              .Include(u => u.Player)
                              .FirstOrDefaultAsync(u => u.Id == userAccountId);
            }
        }

        private static void UpdateSocialAccounts(PasswordLISEntities context, UserAccount userAccountToUpdate,
            List<SocialAccount> updatedSocialsAccounts)
        {
            var existingSocialsLookup = userAccountToUpdate.SocialAccount.ToDictionary(s => s.Provider);
            var providersToKeep = new HashSet<string>(updatedSocialsAccounts.Select(s => s.Provider));

            // Remove socials not in updated list
            var socialsToDelete = userAccountToUpdate.SocialAccount
                .Where(s => !providersToKeep.Contains(s.Provider))
                .ToList();

            foreach (var social in socialsToDelete)
            {
                context.SocialAccount.Remove(social);
            }

            // Handle updates and additions
            foreach (var updatedSocial in updatedSocialsAccounts)
            {
                if (string.IsNullOrWhiteSpace(updatedSocial.Username))
                {
                    // Remove socials with empty username
                    if (existingSocialsLookup.TryGetValue(updatedSocial.Provider, out var existingSocial))
                    {
                        context.SocialAccount.Remove(existingSocial);
                    }
                    continue;
                }

                if (existingSocialsLookup.TryGetValue(updatedSocial.Provider, out var existingSocialToUpdate))
                {
                    if (existingSocialToUpdate.Username != updatedSocial.Username)
                    {
                        existingSocialToUpdate.Username = updatedSocial.Username.Trim();
                        context.Entry(existingSocialToUpdate).State = EntityState.Modified;
                    }
                }
                else
                {
                    updatedSocial.UserAccountId = userAccountToUpdate.Id;
                    updatedSocial.Username = updatedSocial.Username.Trim();
                    context.SocialAccount.Add(updatedSocial);
                }
            }
        }

        public async Task<bool> IsNicknameInUse(string nickname)
        {
            using (var context = contextFactory.CreateDbContext())
            {
                return await context.UserAccount.AnyAsync(p => p.Nickname == nickname);
            }
        }
    }
}
