using Services.Contracts.DTOs;
using System.ServiceModel;

namespace Services.Contracts
{
    [ServiceContract]
    public interface ILoginManager
    {
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        UserDTO Login(string email, string password);

    }
}
