using Data.DAL.Interfaces;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Util;
using System;
using System.ServiceModel;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class PasswordResetManager : IPasswordResetManager
    {
        private readonly IAccountRepository repository;
        private readonly INotificationService notification;
        private readonly IVerificationCodeService codeService;
        public PasswordResetManager(IAccountRepository accountRepository, INotificationService notificationService, IVerificationCodeService verificationCodeService)
        {
            repository = accountRepository;
            notification = notificationService;
            codeService = verificationCodeService;
            
        }
        public bool RequestPasswordResetCode(EmailVerificationDTO email)
        {
            if (!repository.AccountAlreadyExist(email.Email) || !codeService.CanRequestCode(email.Email, CodeType.PasswordReset))
            {
                // No revelamos si el correo no existe por seguridad, pero evitamos el envío.
                // O si está pidiendo códigos demasiado rápido.
                return false;
            }

            var code = codeService.GenerateAndStoreCode(email.Email, CodeType.PasswordReset);
            _ = notification.SendPasswordResetEmailAsync(email.Email, code);

            return true;
        }

        public bool ResetPassword(PasswordResetDTO passwordResetDTO)
        {
            if (codeService.ValidateCode(passwordResetDTO.Email, passwordResetDTO.ResetCode, CodeType.PasswordReset))
            {
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(passwordResetDTO.NewPassword);
                return repository.ResetPassword(passwordResetDTO.Email, hashedPassword);
            }
            return false;
        }
    }
}
