using Data.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Data.DAL.Interfaces
{
    public interface IAccountRepository
    {
        Task CreateAccountAsync(UserAccount account);
        bool AccountAlreadyExist(string email);
        Task<UserAccount> GetUserByEmailAsync(string email);
        bool VerifyEmail(string email);
        bool ResetPassword(string email, string passwordHash);
        Task<bool> UpdateUserProfileAsync(int playerId, UserAccount updatedAccountData,
            List<SocialAccount> updatedSocialsAccounts);
        Task <UserAccount> GetUserByPlayerIdAsync(int playerId);
        Task<UserAccount> GetUserByUserAccountIdAsync(int userAccountId);
        Task<bool> IsNicknameInUse(string nickname);

    }
}
