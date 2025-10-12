using Data.DAL.Interfaces;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Util;
using System.ServiceModel;

namespace Services.Services
{

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class VerificationCodeManager : IAccountVerificationManager
    {
        private readonly IAccountRepository repository;
        private readonly INotificationService notification;
        private readonly IVerificationCodeService codeService;
        public VerificationCodeManager(IAccountRepository accountRepository, INotificationService notificationService,
            IVerificationCodeService verificationCodeService)
        {
            repository = accountRepository;
            notification = notificationService;
            codeService = verificationCodeService;
        }
        public bool VerifyEmail(EmailVerificationDTO emailVerificationDTO)
        {
            bool isCodeValid = codeService.ValidateCode(
                emailVerificationDTO.Email,
                emailVerificationDTO.VerificationCode,
                CodeType.EmailVerification
            );

            if (isCodeValid)
            {
                return repository.VerifyEmail(emailVerificationDTO.Email);
            }

            return false;
        }

        public bool ResendVerificationCode(string email)
        {
            if (!codeService.CanRequestCode(email, CodeType.EmailVerification))
            {
                return false;
            }

            var newCode = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);

            _ = notification.SendAccountVerificationEmailAsync(email, newCode);

            return true;
        }
    }
}
