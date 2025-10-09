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
        private readonly INotificationService notification;
        private readonly IVerificationCodeService codeService;
       

        public AccountManager(IAccountRepository accountRepository, INotificationService notificationService, IVerificationCodeService verificationCodeService)
        {
            repository = accountRepository;
            notification = notificationService;
            codeService = verificationCodeService;
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
                var code = codeService.GenerateAndStoreCode(newAccount.Email, CodeType.EmailVerification);
                _ = notification.SendAccountVerificationEmailAsync(newAccount.Email, code);
                return true;
            }
            
            return false;
        }
    }
}
