using Services.Contracts.DTOs;
using System.ServiceModel;

namespace Services.Contracts
{
    [ServiceContract]
    public interface IProfileManager
    {
        [OperationContract]
        UserDTO UpdateProfile(UserDTO updatedProfileData);
    }
}
