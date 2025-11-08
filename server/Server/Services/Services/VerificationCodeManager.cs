using Data.DAL.Interfaces;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Util;
using System.ServiceModel;
using log4net;
using Services.Contracts.Enums;
using System;

namespace Services.Services
{

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class VerificationCodeManager : IAccountVerificationManager
    {
        private readonly IAccountRepository repository;
        private readonly INotificationService notification;
        private readonly IVerificationCodeService codeService;
        private static readonly ILog log = LogManager.GetLogger(typeof(VerificationCodeManager));
        public VerificationCodeManager(IAccountRepository accountRepository, INotificationService notificationService,
            IVerificationCodeService verificationCodeService)
        {
            repository = accountRepository;
            notification = notificationService;
            codeService = verificationCodeService;
        }
        public bool VerifyEmail(EmailVerificationDTO emailVerificationDTO)
        {
            try
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
            }
            catch (Exception ex)
            {
                log.Error("Unexpected error verifying email.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError, "UNEXPECTED_ERROR", "Unexpected error during email verification.");
            }
        }

        public bool ResendVerificationCode(string email)
        {
            try
            {
                if (!codeService.CanRequestCode(email, CodeType.EmailVerification))
                {
                    log.WarnFormat("Resend verification code denied for '{0}': rate limited or existing valid code.", email);
                    return false;
                }

                var newCode = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);

                _ = notification.SendAccountVerificationEmailAsync(email, newCode);
                log.InfoFormat("Verification code resent to '{0}'.", email);

                return true;
            }
            catch (Exception ex)
            {
                log.Error("Unexpected error resending verification code.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError, "UNEXPECTED_ERROR", "Unexpected error resending verification code.");
            }
        }
    }
}
