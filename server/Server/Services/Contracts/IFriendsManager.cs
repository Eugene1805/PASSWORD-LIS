using Services.Contracts.DTOs;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Contracts
{
    [ServiceContract]
    public interface IFriendsManager
    {
        [OperationContract]
        Task<FriendDTO[]> GetFriendsAsync(int userAccountId);

        [OperationContract]
        Task<bool> DeleteFriendAsync(int currentUserId, int friendToDeleteId);
    }
}
