using Data.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Data.DAL.Interfaces
{
    public interface IAccountRepository
    {
        Task CreateAccountAsync(UserAccount account);
        bool AccountAlreadyExist(string email);
        UserAccount GetUserByEmail(string email);
        bool VerifyEmail(string email);
        bool ResetPassword(string email, string passwordHash);
        bool UpdateUserProfile(int playerId, UserAccount updatedAccountData, List<SocialAccount> updatedSocialsAccounts);
        UserAccount GetUserByPlayerId(int playerId);
        UserAccount GetUserByUserAccountId(int userAccountId);
        Task<bool> IsNicknameInUse(string nickname);

    }
}
