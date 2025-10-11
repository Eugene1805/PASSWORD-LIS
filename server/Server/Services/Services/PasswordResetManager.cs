using Data.DAL.Interfaces;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Util;
using System;
using System.Diagnostics;
using System.ServiceModel;
using System.Threading.Tasks;

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
        public bool RequestPasswordResetCode(EmailVerificationDTO emailVerificationDTO)
        {
            if (!repository.AccountAlreadyExist(emailVerificationDTO.Email) || !codeService.CanRequestCode(emailVerificationDTO.Email, CodeType.PasswordReset))
            {
                return false;
            }
            Console.WriteLine("Se va a enviar el codigo");
            var code = codeService.GenerateAndStoreCode(emailVerificationDTO.Email, CodeType.PasswordReset);
            _ = notification.SendPasswordResetEmailAsync(emailVerificationDTO.Email, code);

            return true;
        }

        public bool ResetPassword(PasswordResetDTO passwordResetDTO)
        {
            
            if (!codeService.ValidateCode(passwordResetDTO.Email, passwordResetDTO.ResetCode, CodeType.PasswordReset))
            {
                Console.WriteLine(" Retorna false porque no se pudo validar el codigo");
                Console.WriteLine("Uso la informacion:" + passwordResetDTO.Email + " " + passwordResetDTO.NewPassword + " " + passwordResetDTO.ResetCode);
                return false;
            }
            Console.WriteLine("Intenta cambiar la contrasena ");
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(passwordResetDTO.NewPassword);
            return repository.ResetPassword(passwordResetDTO.Email, hashedPassword);
        }

        public bool ValidatePasswordResetCode(EmailVerificationDTO emailVerificationDTO)
        {
            return codeService.ValidateCode(emailVerificationDTO.Email, emailVerificationDTO.VerificationCode, CodeType.PasswordReset, consume:false);
        }
    }
}
