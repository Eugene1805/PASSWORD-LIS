using Data.Model;
using System.Collections.Generic;

namespace Data.DAL.Interfaces
{
    public interface IFriendshipRepository
    {
        List<UserAccount> GetFriendsByUserAccountId(int userAccountId);
        bool DeleteFriendship(int currentUserId, int friendToDeleteId);
        bool CreateFriendRequest(int requesterPlayerId, int addresseePlayerId);
        List<Friendship> GetPendingRequests(int userAccountId);
        bool RespondToFriendRequest(int requesterPlayerId, int addresseePlayerId, bool accept);
    }
}
