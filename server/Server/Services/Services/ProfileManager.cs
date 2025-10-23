using Data.DAL.Interfaces;
using Data.Model;
using Services.Contracts;
using Services.Contracts.DTOs;
using System.Linq;
using System.ServiceModel;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class ProfileManager : IProfileManager
    {
        private readonly IAccountRepository repository;

        public ProfileManager(IAccountRepository accountRepository)
        {
            repository = accountRepository;
        }

        public UserDTO UpdateProfile(UserDTO updatedProfileData)
        {
            var accountData = new UserAccount
            {
                FirstName = updatedProfileData.FirstName,
                LastName = updatedProfileData.LastName,
                PhotoId = (updatedProfileData.PhotoId > 0) ? (byte?)updatedProfileData.PhotoId : null
            };

            var socialData = updatedProfileData.SocialAccounts
                .Select(kvp => new SocialAccount { Provider = kvp.Key, Username = kvp.Value })
                .ToList();

            // 3. Llamar al repositorio con los objetos del Modelo
            bool updateSuccess = repository.UpdateUserProfile(
                updatedProfileData.PlayerId,
                accountData,
                socialData
            );

            // Devolvemos el DTO como confirmación si la operación fue exitosa
            return updateSuccess ? updatedProfileData : null;

        }

    }
}
