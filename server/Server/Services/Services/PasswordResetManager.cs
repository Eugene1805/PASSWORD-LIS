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
    public class PasswordResetManager : IPasswordResetManager
    {
        private readonly IAccountRepository repository;
        private readonly INotificationService notification;
        private readonly IVerificationCodeService codeService;
        private static readonly ILog log = LogManager.GetLogger(typeof(PasswordResetManager));
        public PasswordResetManager(IAccountRepository accountRepository, INotificationService notificationService,
            IVerificationCodeService verificationCodeService)
        {
            repository = accountRepository;
            notification = notificationService;
            codeService = verificationCodeService;
            
        }
        public bool RequestPasswordResetCode(EmailVerificationDTO emailVerificationDTO)
        {
            try
            {
                if (emailVerificationDTO == null || string.IsNullOrWhiteSpace(emailVerificationDTO.Email))
                {
                    throw FaultExceptionFactory.Create(
                        ServiceErrorCode.NullArgument,
                        "NULL_ARGUMENT",
                        "Email verification DTO or email is null/empty"
                    );
                }

                if (!repository.AccountAlreadyExist(emailVerificationDTO.Email) ||
                    !codeService.CanRequestCode(emailVerificationDTO.Email, CodeType.PasswordReset))
                {
                    log.WarnFormat("Password reset code request denied for '{0}'. Account missing or rate-limited.", emailVerificationDTO.Email);
                    return false;
                }
                var code = codeService.GenerateAndStoreCode(emailVerificationDTO.Email, CodeType.PasswordReset);
                _ = notification.SendPasswordResetEmailAsync(emailVerificationDTO.Email, code);
                log.InfoFormat("Password reset code sent to '{0}'.", emailVerificationDTO.Email);

                return true;
            }
            catch (ArgumentNullException ex)
            {
                log.Error("Null argument provided to RequestPasswordResetCode.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.NullArgument, "NULL_ARGUMENT", "A null argument was received occurred.");
            }
            catch (Exception ex)
            {
                log.Error("Unexpected error requesting password reset code.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError, "UNEXPECTED_ERROR", "Unexpected error requesting password reset code.");
            }
        }

        public bool ResetPassword(PasswordResetDTO passwordResetDTO)
        {   
            try
            {
                if (passwordResetDTO == null || string.IsNullOrWhiteSpace(passwordResetDTO.Email))
                {
                    throw FaultExceptionFactory.Create(
                        ServiceErrorCode.NullArgument,
                        "NULL_ARGUMENT",
                        "Email verification DTO or email is null/empty"
                    );
                }
                if (!codeService.ValidateCode(passwordResetDTO.Email, passwordResetDTO.ResetCode, CodeType.PasswordReset))
                {
                    log.WarnFormat("Password reset failed for '{0}': invalid or expired code.", passwordResetDTO.Email);
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
            }
            catch (ArgumentNullException ex)
            {
                log.Error("Null argument provided to CreateAccountAsync.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.NullArgument, "NULL_ARGUMENT", "A null argument was received occurred.");
            }
            catch (Exception ex)
            {
                log.Error("Unexpected error resetting password.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError, "UNEXPECTED_ERROR", "Unexpected error resetting password.");
            }
        }

        public bool ValidatePasswordResetCode(EmailVerificationDTO emailVerificationDTO)
        {
            try
            {
                if (emailVerificationDTO == null || string.IsNullOrWhiteSpace(emailVerificationDTO.Email))
                {
                    throw FaultExceptionFactory.Create(
                        ServiceErrorCode.NullArgument,
                        "NULL_ARGUMENT",
                        "Email verification DTO or email is null/empty"
                    );
                }
                var ok = codeService.ValidateCode(emailVerificationDTO.Email, emailVerificationDTO.VerificationCode, 
                    CodeType.PasswordReset, consume:false);
                if (!ok)
                {
                    log.WarnFormat("Password reset code validation failed for '{0}'.", emailVerificationDTO.Email);
                }
                else
                {
                    log.InfoFormat("Password reset code validation succeeded for '{0}'.", emailVerificationDTO.Email);
                }
                return ok;
            }
            catch (ArgumentNullException ex)
            {
                log.Error("Null argument provided to CreateAccountAsync.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.NullArgument, "NULL_ARGUMENT", "A null argument was received occurred.");
            }
            catch (Exception ex)
            {
                log.Error("Unexpected error validating password reset code.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError, "UNEXPECTED_ERROR", "Unexpected error validating password reset code.");
            }
        }
    }
}
