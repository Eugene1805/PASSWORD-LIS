using Services.Contracts.DTOs;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Contracts
{
    /// <summary>
    /// Exposes account-related operations such as creating users and nickname validation.
    /// </summary>
    [ServiceContract]
    public interface IAccountManager
    {
        /// <summary>
        /// Creates a new user account.
        /// </summary>
        /// <param name="newAccount">The account information to create.</param>
        /// <returns>An asynchronous operation.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task CreateAccountAsync(NewAccountDTO newAccount);

        /// <summary>
        /// Checks whether a nickname is already in use.
        /// </summary>
        /// <param name="nickname">The nickname to check.</param>
        /// <returns>True if the nickname is taken; otherwise, false.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task<bool> IsNicknameInUse(string nickname);
    }
}
