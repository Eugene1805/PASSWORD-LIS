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
            // Paso 1: Pedirle al especialista en códigos que valide el código.
            bool isCodeValid = codeService.ValidateCode(
                emailVerificationDTO.Email,
                emailVerificationDTO.VerificationCode,
                CodeType.EmailVerification
            );

            if (isCodeValid)
            {
                // Paso 2: Si el código es válido, pedirle al especialista en datos que actualice la BD.
                return repository.VerifyEmail(emailVerificationDTO.Email);
            }

            return false;
        }

        public bool ResendVerificationCode(string email)
        {
            // Paso 1: Preguntarle al especialista si el usuario puede solicitar un nuevo código (throttling).
            if (!codeService.CanRequestCode(email, CodeType.EmailVerification))
            {
                return false; // Indicar al cliente que debe esperar.
            }

            // Paso 2: Pedirle al especialista que genere y guarde un nuevo código.
            var newCode = codeService.GenerateAndStoreCode(email, CodeType.EmailVerification);

            // Paso 3: Pedirle al especialista en notificaciones que envíe el correo.
            _ = notification.SendAccountVerificationEmailAsync(email, newCode);

            return true;
        }
    }
}
