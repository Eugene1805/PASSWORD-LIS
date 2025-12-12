using Data.DAL.Interfaces;
using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Util;
using System.ServiceModel;

namespace Services.Services
{

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class VerificationCodeManager : ServiceBase,IAccountVerificationManager
    {
        private readonly IAccountRepository repository;
        private readonly INotificationService notification;
        private readonly IVerificationCodeService codeService;
        private static readonly ILog log = LogManager.GetLogger(typeof(VerificationCodeManager));
        public VerificationCodeManager(IAccountRepository AccountRepository, INotificationService NotificationService,
            IVerificationCodeService VerificationCodeService) : base(log)
        {
            repository = AccountRepository;
            notification = NotificationService;
            codeService = VerificationCodeService;
        }
        public bool VerifyEmail(EmailVerificationDTO emailVerificationDTO)
        {
            return Execute(()=>
            {
                bool isCodeValid = codeService.ValidateCode(
                    emailVerificationDTO.Email,
                    emailVerificationDTO.VerificationCode,
                    CodeType.EmailVerification
                );

                if (isCodeValid)
                {
                    log.InfoFormat("Email verification succeeded for '{0}'.", emailVerificationDTO.Email);
                    return repository.VerifyEmail(emailVerificationDTO.Email);
                }

                log.WarnFormat("Email verification failed: invalid code for '{0}'.", emailVerificationDTO.Email);
                return false;
            }, context: "VerifyEmail: VerificationCodeManager");
        }

        public bool ResendVerificationCode(string email)
        {
            return Execute(()=>
            {
                if (!codeService.CanRequestCode(email, CodeType.EmailVerification))
                {
                    log.WarnFormat("Resend verification code denied for '{0}': rate limited or existing valid code.",
                        email);
                    return false;
                }

                var newCode = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);

                _ = notification.SendAccountVerificationEmailAsync(email, newCode);
                log.InfoFormat("Verification code resent to '{0}'.", email);

                return true;
            }, context: "ResendVerificationCode: VerificationCodeManager");
        }
    }
}
