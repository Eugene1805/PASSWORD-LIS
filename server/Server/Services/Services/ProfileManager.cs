using Data.DAL.Interfaces;
using Services.Contracts;
using Services.Contracts.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

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
            bool updateSuccess = repository.UpdateUserProfile(
                updatedProfileData.PlayerId, 
                updatedProfileData.Nickname, 
                updatedProfileData.FirstName, 
                updatedProfileData.LastName, 
                updatedProfileData.PhotoId,
                updatedProfileData.SocialAccounts
             );

            return updateSuccess ? updatedProfileData : null;
        }
    }
}
