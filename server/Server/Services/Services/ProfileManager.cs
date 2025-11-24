using Data.DAL.Interfaces;
using Data.Model;
using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class ProfileManager : ServiceBase, IProfileManager
    {
        private readonly IAccountRepository repository;
        private static readonly ILog log = LogManager.GetLogger(typeof(ProfileManager));

        public ProfileManager(IAccountRepository accountRepository) :base(log)
        {
            repository = accountRepository;
        }

        public async Task<UserDTO> UpdateProfileAsync(UserDTO updatedProfileData)
        {
            return await ExecuteAsync(async () =>
            {
                if (updatedProfileData == null || updatedProfileData.PlayerId <= 0)
                {
                    log.WarnFormat("UpdateProfile called with null data or invalid PlayerId {0}",
                        updatedProfileData?.PlayerId);
                    return null;
                }
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

            }, context:"ProfileManager: UpdateProfileAsync");
            
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
