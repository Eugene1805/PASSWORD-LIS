using Data.Model;
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

        bool UpdateUserAvatar(int playerId, int newPhotoId);
    }
}
