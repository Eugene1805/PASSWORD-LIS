using Data.DAL.Interfaces;
using Data.Model;
using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using System;
using System.Data.Common;
using System.Linq;
using System.ServiceModel;
using Services.Util;
using System.Threading.Tasks;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class LoginManager : ServiceBase, ILoginManager
    {
        private readonly IAccountRepository repository;
        private readonly INotificationService notification;
        private readonly IVerificationCodeService codeService;
        private static readonly ILog log = LogManager.GetLogger(typeof(LoginManager));

        public LoginManager(IAccountRepository accountRepository, INotificationService notificationService,
            IVerificationCodeService verificationCodeService) :base(log)
        {
            repository = accountRepository;
            notification = notificationService;
            codeService = verificationCodeService;
        }

        public async Task<UserDTO> LoginAsync(string email, string password)
        {
            return await ExecuteAsync(async () =>
            {
                log.InfoFormat("Login attempt for email: {0}", email);
                var userAccount = await repository.GetUserByEmailAsync(email);

                if (userAccount == null)
                {
                    log.WarnFormat("Login failed (user not found) for: {0}", email);
                    return new UserDTO { UserAccountId = -1 };
                }

                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, userAccount.PasswordHash);

                if (isPasswordValid && userAccount.Player.Any())
                {
                    var player = userAccount.Player.FirstOrDefault();
                    if (player != null)
                    {
                        log.InfoFormat("Login succeeded for: {0}, PlayerId: {1}", email, player.Id);
                        return MapUserToDTO(userAccount, player);
                    }
                }
                log.WarnFormat("Login failed (wrong password or no player) for: {0}", email);
                return new UserDTO { UserAccountId = -1 };
            }, context: "LoginManager: LoginAsync");
        }

        public async Task<bool> IsAccountVerifiedAsync(string email)
        {
            return await ExecuteAsync(async () =>
            {
                log.InfoFormat("Verification check for email: {0}", email);
                var userAccount = await repository.GetUserByEmailAsync(email);
                if (userAccount == null)
                {
                    log.WarnFormat("Verification check failed (user not found) for: {0}", email);
                    return false;
                }
                log.InfoFormat("Verification check succeeded for: {0}, IsVerified: {1}",
                    email, userAccount.EmailVerified);
                return userAccount.EmailVerified;
            }, context: "LoginManager: IsAccountVerifiedAsync");
        }
        private UserDTO MapUserToDTO(UserAccount userAccount, Player player)
        {
            return new UserDTO
            {
                UserAccountId = userAccount.Id,
                PlayerId = player.Id,
                Nickname = userAccount.Nickname,
                Email = userAccount.Email,
                FirstName = userAccount.FirstName,
                LastName = userAccount.LastName,
                PhotoId = userAccount.PhotoId ?? 0,
                SocialAccounts = userAccount.SocialAccount.ToDictionary(sa => sa.Provider, sa => sa.Username)
            };
        }
        public async Task SendVerificationCodeAsync(string email)
        {
            await ExecuteAsync(async () =>
            {
                var userAccount = await repository.GetUserByEmailAsync(email);
                if (userAccount != null && !userAccount.EmailVerified)
                {
                    SendEmailVerification(email);
                }
            }, context: "LoginManager: SendVerificationCodeAsync");
        }
        private void SendEmailVerification(string email)
        {
            var code = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);
            _ = notification.SendAccountVerificationEmailAsync(email, code);
        }
    }
}
