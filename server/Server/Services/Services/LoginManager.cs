using Data.DAL.Interfaces;
using Data.Model;
using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
using System;
using System.Data.Common;
using System.Linq;
using System.ServiceModel;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class LoginManager : ILoginManager
    {
        public readonly IAccountRepository repository;
        public static readonly ILog log = LogManager.GetLogger(typeof(LoginManager));

        public LoginManager(IAccountRepository accountRepository)
        {
            repository = accountRepository;
        }

        public UserDTO Login(string email, string password)
        {
            try
            {
                log.InfoFormat("Login attempt for email: {0}", email);
                var userAccount = repository.GetUserByEmail(email);

                if (userAccount == null)
                {
                    log.WarnFormat("Login failed (user not found) for: {0}", email);
                    return null;
                }

                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, userAccount.PasswordHash);

                if (isPasswordValid && userAccount.Player.Any())
                {
                    var player = userAccount.Player.FirstOrDefault();
                    if (player != null)
                    {
                        log.InfoFormat("Login succeeded for: {0}, PlayerId: {1}", email, player.Id);
                        return MapUserToDTO(userAccount, player);
                    }
                }
                log.WarnFormat("Login failed (wrong password or no player) for: {0}", email);
                return null;
            }
            catch (DbException dbEx)
            {
                log.Error("Database error (DbException) during login.", dbEx);
                var errorDetail = new ServiceErrorDetailDTO
                {
                    Code = ServiceErrorCode.DatabaseError,
                    ErrorCode = "DATABASE_ERROR",
                    Message = "An error occurred while querying the database. Please try again later."
                };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message));
            } catch (Exception ex)
            {
                log.Fatal("Unexpected fatal error in Login.", ex);
                var errorDetail = new ServiceErrorDetailDTO
                {
                    Code = ServiceErrorCode.UnexpectedError,
                    ErrorCode = "UNEXPECTED_ERROR",
                    Message = "An unexpected server error occurred."
                };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message));
            }
        }
        private UserDTO MapUserToDTO(UserAccount userAccount, Player player)
        {
            return new UserDTO
            {
                UserAccountId = userAccount.Id,
                PlayerId = player.Id,
                Nickname = userAccount.Nickname,
                Email = userAccount.Email,
                FirstName = userAccount.FirstName,
                LastName = userAccount.LastName,
                PhotoId = userAccount.PhotoId ?? 0,
                SocialAccounts = userAccount.SocialAccount.ToDictionary(sa => sa.Provider, sa => sa.Username)
            };
        }
    }
}
