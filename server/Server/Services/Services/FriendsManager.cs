using Data.DAL.Implementations;
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
    public class FriendsManager : IFriendsManager
    {
        private readonly IFriendshipRepository repository;

        public FriendsManager(IFriendshipRepository friendshipRepository)
        {
            repository = friendshipRepository;
        }

        
        public Task<FriendDTO[]> GetFriendsAsync(int userAccountId)
        {
            var friendAccounts = repository.GetFriendsByUserAccountId(userAccountId);

            var friendDTOs = friendAccounts.Select(acc => new FriendDTO
            {
                PlayerId = acc.Player.First().Id,
                Nickname = acc.Nickname,
            }).ToArray();

            return Task.FromResult(friendDTOs);
        }

        public Task<bool> DeleteFriendAsync(int currentUserId, int friendToDeleteId)
        {
            bool success = repository.DeleteFriendship(currentUserId, friendToDeleteId);
            return Task.FromResult(success);
        }
    }
}
