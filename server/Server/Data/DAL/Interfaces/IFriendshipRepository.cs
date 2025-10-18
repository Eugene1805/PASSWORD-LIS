using Data.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.DAL.Interfaces
{
    public interface IFriendshipRepository
    {
        List<UserAccount> GetFriendsByUserAccountId(int userAccountId);
    }
}
