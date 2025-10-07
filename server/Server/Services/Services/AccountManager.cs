using Services.Contracts.DTOs;
using Services.Contracts;
using Data.DAL;
using Data.Model;

namespace Services.Services
{
    public class AccountManager : IAccountManager
    {
        public bool CreateAccount(NewAccountDTO newAccount)
        {
            var repository = new AccountRepository();
            var userAccount = new UserAccount
            {
                FirstName = newAccount.FirstName,
                LastName = newAccount.LastName,
                Email = newAccount.Email,
                PasswordHash = null,
                Nickname = newAccount.Nickname
            };

            return repository.CreateAccount(userAccount);
        }
    }
}
