using Services.Contracts.DTOs;
using System.ServiceModel;

namespace Services.Contracts
{
    [ServiceContract]
    public interface IProfileManager
    {
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        UserDTO UpdateProfile(UserDTO updatedProfileData);
    }
}
