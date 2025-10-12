using Services.Contracts.DTOs;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Contracts
{
    [ServiceContract]
    public interface IAccountManager
    {
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task CreateAccountAsync(NewAccountDTO newAccount);
    }
}
