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
                log.WarnFormat("UpdateProfile called with null data or invalid PlayerId {0}",
                    updatedProfileData?.PlayerId);
                return null;
            }

            try
            {
                log.InfoFormat("Attempting to update profile for PlayerId: {0}", updatedProfileData.PlayerId);

                var accountData = MapToUserAccount(updatedProfileData);
                var socialData = MapToSocialAccounts(updatedProfileData.SocialAccounts);

                bool updateSuccess = await repository.UpdateUserProfileAsync(
                    updatedProfileData.PlayerId,
                    accountData,
                    socialData
                );

                if (updateSuccess)
                {
                    log.InfoFormat("Profile updated successfully for PlayerId: {0}", updatedProfileData.PlayerId);
                    return updatedProfileData;
                }
                else
                {
                    log.WarnFormat("Profile update failed (repository returned false) for PlayerId: {0}",
                        updatedProfileData.PlayerId);
                    return null;
                }
            }
            catch (DbUpdateException dbUpEx)
            {
                log.Error($"DbUpdateException updating profile PlayerId: {updatedProfileData.PlayerId}." +
                    $" Ex: {dbUpEx.Message}", dbUpEx);
                throw FaultExceptionFactory.Create(ServiceErrorCode.DatabaseError, "DATABASE_ERROR",
                    "An error occurred while trying to save the changes to the database.");
            }
            catch (DbException dbEx)
            {
                log.Error($"DbException updating profile PlayerId: {updatedProfileData.PlayerId}. " +
                    $"Ex: {dbEx.Message}", dbEx);
                throw FaultExceptionFactory.Create( ServiceErrorCode.DatabaseError, "DATABASE_ERROR", 
                    "A database communication error occurred while updating the profile.");
            }
            catch (Exception ex)
            {
                log.Fatal($"Unexpected error in UpdateProfile for PlayerId: {updatedProfileData.PlayerId}. " +
                    $"Ex: {ex.Message}", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError, "UNEXPECTED_ERROR", 
                    "An unexpected server error occurred while updating the profile.");
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
