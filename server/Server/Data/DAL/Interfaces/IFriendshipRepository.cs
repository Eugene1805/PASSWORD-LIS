using Data.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Data.DAL.Interfaces
{
    public interface IFriendshipRepository
    {
        Task<List<UserAccount>> GetFriendsByUserAccountIdAsync(int userAccountId);
        Task<bool> DeleteFriendshipAsync(int currentUserId, int friendToDeleteId);
        Task<bool> CreateFriendRequestAsync(int requesterPlayerId, int addresseePlayerId);
        Task<List<Friendship>> GetPendingRequestsAsync(int userAccountId);
        Task<bool> RespondToFriendRequestAsync(int requesterPlayerId, int addresseePlayerId, bool accept);
    }
}
