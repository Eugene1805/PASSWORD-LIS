using Data.DAL.Implementations;
using Data.DAL.Interfaces;
using Data.Model;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Util;
using System.ServiceModel;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class AccountManager : IAccountManager
    {
        private readonly IAccountRepository repository;
        private readonly IEmailSender sender;
       

        public AccountManager(IAccountRepository accountRepository, IEmailSender emailSender)
        {
            repository = accountRepository;
            sender = emailSender;
        }

        public bool CreateAccount(NewAccountDTO newAccount)
        {
            
            var userAccount = new UserAccount
            {
                FirstName = newAccount.FirstName,
                LastName = newAccount.LastName,
                Email = newAccount.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(newAccount.Password),
                Nickname = newAccount.Nickname,
            };
            if (repository.CreateAccount(userAccount))
            {
                var verificationCode = VerificationCodeManager.GenerateCode(newAccount.Email);

                _ = sender.SendVerificationEmailAsync(newAccount.Email, verificationCode);
                return true;
            }
            
            return false;
        }
    }
}
