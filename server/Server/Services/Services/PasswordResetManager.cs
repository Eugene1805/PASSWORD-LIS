using Data.DAL.Interfaces;
using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Util;
using System.ServiceModel;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class PasswordResetManager : ServiceBase,IPasswordResetManager
    {
        private readonly IAccountRepository repository;
        private readonly INotificationService notification;
        private readonly IVerificationCodeService codeService;
        private static readonly ILog log = LogManager.GetLogger(typeof(PasswordResetManager));
        public PasswordResetManager(IAccountRepository accountRepository, INotificationService notificationService,
            IVerificationCodeService verificationCodeService) : base(log)
        {
            repository = accountRepository;
            notification = notificationService;
            codeService = verificationCodeService;
            
        }
        public bool RequestPasswordResetCode(EmailVerificationDTO emailVerificationDTO)
        {
            return Execute(() =>
            {
                if (emailVerificationDTO == null || string.IsNullOrWhiteSpace(emailVerificationDTO.Email))
                {
                    return false;
                }

                if (!repository.AccountAlreadyExist(emailVerificationDTO.Email) ||
                    !codeService.CanRequestCode(emailVerificationDTO.Email, CodeType.PasswordReset))
                {
                    log.WarnFormat("Password reset code request denied for '{0}'. Account missing or rate-limited.",
                        emailVerificationDTO.Email);
                    return false;
                }
                SendEmailVerification(emailVerificationDTO.Email);

                return true;
            }, context: "PasswordResetManager: RequestPasswordResetCode");            
        }

        public bool ResetPassword(PasswordResetDTO passwordResetDTO)
        {
            return Execute(() =>
            {
                if (passwordResetDTO == null || string.IsNullOrWhiteSpace(passwordResetDTO.Email))
                {
                    return false;
                }
                if (!codeService.ValidateCode(passwordResetDTO.Email, passwordResetDTO.ResetCode,
                    CodeType.PasswordReset))
                {
                    log.WarnFormat("Password reset failed for '{0}': invalid or expired code.",
                        passwordResetDTO.Email);
                    return false;
                }
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(passwordResetDTO.NewPassword);
                var result = repository.ResetPassword(passwordResetDTO.Email, hashedPassword);
                if (result)
                {
                    log.InfoFormat("Password reset succeeded for '{0}'.", passwordResetDTO.Email);
                }
                else
                {
                    log.WarnFormat("Password reset repository update failed for '{0}'.", passwordResetDTO.Email);
                }
                return result;
            },context:"PasswordResetManager: ResetPassword");
        }

        public bool ValidatePasswordResetCode(EmailVerificationDTO emailVerificationDTO)
        {
            return Execute(() =>
            {
                if (emailVerificationDTO == null || string.IsNullOrWhiteSpace(emailVerificationDTO.Email))
                {
                    return false;
                }
                var ok = codeService.ValidateCode(emailVerificationDTO.Email, emailVerificationDTO.VerificationCode,
                    CodeType.PasswordReset, consume: false);
                if (!ok)
                {
                    log.WarnFormat("Password reset code validation failed for '{0}'.", emailVerificationDTO.Email);
                }
                else
                {
                    log.InfoFormat("Password reset code validation succeeded for '{0}'.", emailVerificationDTO.Email);
                }
                return ok;
            }, context:"ResetPasswordManager: ValidatePasswordResetCode");
        }

        private void SendEmailVerification(string email)
        {
            var code = codeService.GenerateAndStoreCode(email, CodeType.PasswordReset);
            _ = notification.SendPasswordResetEmailAsync(email, code);
            log.InfoFormat("Password reset code sent to '{0}'.", email);
        }
    }
}
