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
                log.InfoFormat("Intento de Login para el email: {0}", email);
                var userAccount = repository.GetUserByEmail(email);

                if (userAccount == null)
                {
                    log.WarnFormat("Login fallido (usuario no encontrado) para : {0}", email);
                    return null;
                }

                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, userAccount.PasswordHash);

                if (isPasswordValid && userAccount.Player.Any())
                {
                    var player = userAccount.Player.FirstOrDefault();
                    if (player != null)
                    {
                        log.InfoFormat("Login exitoso para: {0}, PlayerId: {1}", email, player.Id);
                        return MapUserToDTO(userAccount, player);
                    }
                }
                log.WarnFormat("Login fallido (contraseña incorrecta o sin player) para: {0}", email);
                return null;
            }
            catch (DbException dbEx)
            {
                log.Error("Error de base de datos (DbException) durante el login.", dbEx);
                var errorDetail = new ServiceErrorDetailDTO
                {
                    ErrorCode = "DATABASE_ERROR",
                    Message = "Ocurrió un error al consultar la base de datos. Por favor, inténtalo más tarde."
                };
                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message));
            } catch (Exception ex)
            {
                log.Fatal("Error fatal inesperado en Login.", ex);
                var errorDetail = new ServiceErrorDetailDTO
                {
                    ErrorCode = "UNEXPECTED_ERROR",
                    Message = "Ocurrió un error inesperado en el servidor."
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
