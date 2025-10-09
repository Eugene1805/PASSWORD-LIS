using Data.DAL.Interfaces;
using Services.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    internal class ProfileManager : IProfileManager
    {
        private readonly IAccountRepository repository;

        public ProfileManager(IAccountRepository accountRepository)
        {
            repository = accountRepository;
        }

        public bool UpdateAvatar(int playerId, int newPhotoId)
        {
            return repository.UpdateUserAvatar(playerId, newPhotoId);
        }
    }
    {
    }
}
