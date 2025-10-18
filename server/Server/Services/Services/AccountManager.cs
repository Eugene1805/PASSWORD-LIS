using Data.DAL.Interfaces;
using Data.Exceptions;
using Data.Model;
using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Util;
using System;
using System.Data.Entity.Infrastructure;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class AccountManager : IAccountManager
    {
        private readonly IAccountRepository repository;
        private readonly INotificationService notification;
        private readonly IVerificationCodeService codeService;

        private static readonly ILog log = LogManager.GetLogger(typeof(AccountManager));


        public AccountManager(IAccountRepository accountRepository, INotificationService notificationService,
            IVerificationCodeService verificationCodeService)
        {
            repository = accountRepository;
            notification = notificationService;
            codeService = verificationCodeService;
        }

        public async Task CreateAccountAsync(NewAccountDTO newAccount)
        {
            try
            {
                log.InfoFormat("Intentando crear cuenta para el email: {0}", newAccount.Email);
                var userAccount = new UserAccount
                {
                    FirstName = newAccount.FirstName,
                    LastName = newAccount.LastName,
                    Email = newAccount.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(newAccount.Password),
                    Nickname = newAccount.Nickname,
                };
                await repository.CreateAccountAsync(userAccount);
                log.InfoFormat("Cuenta creada exitosamente para: '{0}'", userAccount.Email);
                var code = codeService.GenerateAndStoreCode(newAccount.Email, CodeType.EmailVerification);
                _ = notification.SendAccountVerificationEmailAsync(newAccount.Email, code);
            }
            catch (DuplicateAccountException ex)
            {
                log.Warn($"Intento de registro duplicado para el email: {newAccount.Email}", ex);
                var errorDetail = new ServiceErrorDetailDTO
                {
                    ErrorCode = "USER_ALREADY_EXISTS",
                    Message = ex.Message 
                };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message));
            }
            catch (DbUpdateException dbEx)
            {
                log.Error("Error en la base de datos al crear la cuenta.", dbEx);
                var errorDetail = new ServiceErrorDetailDTO
                {
                    ErrorCode = "DATABASE_ERROR",
                    Message = "Ocurrió un error al procesar tu solicitud. Por favor, inténtalo más tarde."
                };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason("Error en la capa de datos."));
            }
            catch (Exception ex)
            {
                log.Fatal("Error fatal inesperado en CreateAccount.", ex);
                var errorDetail = new ServiceErrorDetailDTO
                {
                    ErrorCode = "UNEXPECTED_ERROR",
                    Message = "Ocurrió un error inesperado en el servidor."
                };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason("Error genérico del servidor."));
            }

        }
    }
}
