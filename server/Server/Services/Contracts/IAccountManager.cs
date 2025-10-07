using Services.Contracts.DTOs;
using System.ServiceModel;

namespace Services.Contracts
{
    [ServiceContract]
    public interface IAccountManager
    {
        [OperationContract]
        bool CreateAccount(NewAccountDTO newAccount);
    }
}
