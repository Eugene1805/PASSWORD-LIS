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
            catch(ArgumentNullException ex)
            {
                log.Error("Null argument provided to CreateAccountAsync.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.NullArgument, "NULL_ARGUMENT", "A null argument was received occurred.");
            }
            catch (DuplicateAccountException ex)
            {
                log.WarnFormat("Duplicated registry attempt for the email: {0}", newAccount.Email);
                log.Warn("DuplicateAccountException thrown while creating account.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UserAlreadyExists, "USER_ALREADY_EXISTS", ex.Message);
            }
            catch (DbUpdateException dbEx)
            {
                log.Error("Error at the dababase when creating the account.", dbEx);
                throw FaultExceptionFactory.Create(ServiceErrorCode.DatabaseError, "DATABASE_ERROR", "An error occurred while processing the request.");
            }
            catch (Exception ex)
            {
                log.Fatal("Unexpected fatal error in CreateAccount.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError, "UNEXPECTED_ERROR", "An unexpected server error occurred.");
            }

        }

        public async Task<bool> IsNicknameInUse(string nickname)
        {
            if (nickname is null)
            {
                return false;
            }
            try
            {
                return await repository.IsNicknameInUse(nickname);
            }
            catch(InvalidOperationException ex)
            {
                log.Error("Invalid operation error in IsNickNameInUse.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.DatabaseError, "DATABASE_ERROR", "An error occurred while querying the database");
            }
            catch (Exception ex)
            {
                log.Fatal("Unexpected fatal error in IsNickNameInUse.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError, "UNEXPECTED_ERROR", "An unexpected server error occurred.");
            }
            
        }
    }
}
