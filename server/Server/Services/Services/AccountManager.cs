using Services.Contracts.DTOs;
using Services.Contracts;
using Data.DAL;
using Data.Model;
using Services.Util;

namespace Services.Services
{
    public class AccountManager : IAccountManager
    {
        public bool CreateAccount(NewAccountDTO newAccount)
        {
            
            var userAccount = new UserAccount
            {
                FirstName = newAccount.FirstName,
                LastName = newAccount.LastName,
                Email = newAccount.Email,
                PasswordHash = newAccount.Password,
                Nickname = newAccount.Nickname,
            };
            if (AccountRepository.CreateAccount(userAccount))
            {
                var verificationCode = VerificationCodeManager.GenerateCode(newAccount.Email);

                _ = EmailSender.SendVerificationEmailAsync(newAccount.Email, verificationCode);
                return true;
            }
            
            return false;
        }
    }
}
