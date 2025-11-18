using Data.DAL.Interfaces;
using Data.Model;
using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Services.Util;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class ProfileManager : IProfileManager
    {
        private readonly IAccountRepository repository;
        private static readonly ILog log = LogManager.GetLogger(typeof(ProfileManager));

        public ProfileManager(IAccountRepository accountRepository)
        {
            repository = accountRepository;
        }

        public async Task<UserDTO> UpdateProfileAsync(UserDTO updatedProfileData)
        {
            if (updatedProfileData == null || updatedProfileData.PlayerId <= 0)
            {
                log.WarnFormat("UpdateProfile llamado con datos nulos o PlayerId inválido {0}" ,updatedProfileData?.PlayerId);
                return null;
            }

            try
            {
                log.InfoFormat("Intentando actualizar perfil para PlayerId: {0}", updatedProfileData.PlayerId);

                var accountData = MapToUserAccount(updatedProfileData);
                var socialData = MapToSocialAccounts(updatedProfileData.SocialAccounts);

                bool updateSuccess = await repository.UpdateUserProfileAsync(
                    updatedProfileData.PlayerId,
                    accountData,
                    socialData
                );

                if (updateSuccess)
                {
                    log.InfoFormat("Perfil actualizado exitosamente para PlayerId: {0}", updatedProfileData.PlayerId);
                    return updatedProfileData;
                }
                else
                {
                    log.WarnFormat("Falló la actualización del perfil (repositorio devolvió false) para PlayerId: {0}", updatedProfileData.PlayerId);
                    return null;
                }
            }
            catch (DbUpdateException dbUpEx)
            {
                log.Error($"Error DbUpdateException al actualizar perfil PlayerId: {updatedProfileData.PlayerId}. Ex: {dbUpEx.Message}", dbUpEx);
                throw FaultExceptionFactory.Create(ServiceErrorCode.DatabaseError, "DATABASE_ERROR", "Ocurrió un error al intentar guardar los cambios en la base de datos.");
            }
            catch (DbException dbEx)
            {
                log.Error($"Error DbException al actualizar perfil PlayerId: {updatedProfileData.PlayerId}. Ex: {dbEx.Message}", dbEx);
                throw FaultExceptionFactory.Create( ServiceErrorCode.DatabaseError, "DATABASE_ERROR", "Ocurrió un error de comunicación con la base de datos.");
            }
            catch (Exception ex)
            {
                log.Fatal($"Error inesperado en UpdateProfile para PlayerId: {updatedProfileData.PlayerId}. Ex: {ex.Message}", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError, "UNEXPECTED_ERROR", "Ocurrió un error inesperado en el servidor al actualizar el perfil.");
            }
        }

        private UserAccount MapToUserAccount(UserDTO dto)
        {
            return new UserAccount
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                PhotoId = (dto.PhotoId > 0) ? (byte?)dto.PhotoId : null
            };
        }

        private List<SocialAccount> MapToSocialAccounts(Dictionary<string, string> socialAccountsDict)
        {
            if (socialAccountsDict == null)
            {
                return new List<SocialAccount>();
            }
            return socialAccountsDict
                .Select(kvp => new SocialAccount { Provider = kvp.Key, Username = kvp.Value })
                .ToList();
        }
    }
}
