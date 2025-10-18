using Data.Model;
using System;
using System.Collections.Generic;

namespace Data.DAL.Interfaces
{
    public interface IFriendshipRepository
    {
        List<UserAccount> GetFriendsByUserAccountId(int userAccountId);

        bool DeleteFriendship(int currentUserId, int friendToDeleteId);
    }
}
