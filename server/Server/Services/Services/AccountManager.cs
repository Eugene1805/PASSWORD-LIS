using Data.DAL.Interfaces;
using Data.Model;
using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Util;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class AccountManager : ServiceBase, IAccountManager
    {
        private readonly IAccountRepository repository;
        private readonly INotificationService notification;
        private readonly IVerificationCodeService codeService;

        private static readonly ILog log = LogManager.GetLogger(typeof(AccountManager));


        public AccountManager(IAccountRepository accountRepository, INotificationService notificationService,
            IVerificationCodeService verificationCodeService) : base(log)
        {
            repository = accountRepository;
            notification = notificationService;
            codeService = verificationCodeService;
        }

        public async Task CreateAccountAsync(NewAccountDTO newAccount)
        {
            await ExecuteAsync(async () =>
            {
                log.InfoFormat("Trying to create account for the email: {0}", newAccount.Email);
                var userAccount = new UserAccount
                {
                    FirstName = newAccount.FirstName,
                    LastName = newAccount.LastName,
                    Email = newAccount.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(newAccount.Password),
                    Nickname = newAccount.Nickname,
                };
                await repository.CreateAccountAsync(userAccount);
                log.InfoFormat("Account succesfully created for: '{0}'", userAccount.Email);
                await SendEmailVerification(userAccount.Email);
            }, context: "AccountManager: CreateAccountAsync");
        }

        public async Task<bool> IsNicknameInUse(string nickname)
        {
            if (nickname is null)
            {
                return false;
            }
            return await ExecuteAsync(() =>
            {
                return repository.IsNicknameInUse(nickname);
            }, context: "AccountManager: IsNickNameInUse");
        }

        private async Task SendEmailVerification(string email)
        {
            var code = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);
            await notification.SendAccountVerificationEmailAsync(email, code);
        }
    }
}
