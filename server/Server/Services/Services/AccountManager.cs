using Data.DAL.Interfaces;
using Data.Exceptions;
using Data.Model;
using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
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
                log.InfoFormat("Trying to create account for the email: {0}", newAccount.Email);
                var userAccount = new UserAccount
                {
                    FirstName = newAccount.FirstName,
                    LastName = newAccount.LastName,
                    Email = newAccount.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(newAccount.Password),
                    Nickname = newAccount.Nickname,
                };
                await repository.CreateAccountAsync(userAccount);
                log.InfoFormat("Account succesfully created for: '{0}'", userAccount.Email);
                var code = codeService.GenerateAndStoreCode(newAccount.Email, CodeType.EmailVerification);
                _ = notification.SendAccountVerificationEmailAsync(newAccount.Email, code);
            }
            catch (DuplicateAccountException ex)
            {
                log.WarnFormat("Duplicated registry attempt for the email: {0}", newAccount.Email);
                log.Warn("DuplicateAccountException thrown while creating account.", ex);
                var errorDetail = new ServiceErrorDetailDTO
                {
                    Code = ServiceErrorCode.UserAlreadyExists,
                    ErrorCode = "USER_ALREADY_EXISTS",
                    Message = ex.Message 
                };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message));
            }
            catch (DbUpdateException dbEx)
            {
                log.Error("Error at the dababase when creating the account.", dbEx);
                var errorDetail = new ServiceErrorDetailDTO
                {
                    Code = ServiceErrorCode.DatabaseError,
                    ErrorCode = "DATABASE_ERROR",
                    Message = "An error occurred while processing your request. Please try again later."
                };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason("Data layer error."));
            }
            catch (Exception ex)
            {
                log.Fatal("Unexpected fatal error in CreateAccount.", ex);
                var errorDetail = new ServiceErrorDetailDTO
                {
                    Code = ServiceErrorCode.UnexpectedError,
                    ErrorCode = "UNEXPECTED_ERROR",
                    Message = "An unexpected server error occurred."
                };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason("Generic server error."));
            }

        }
    }
}
