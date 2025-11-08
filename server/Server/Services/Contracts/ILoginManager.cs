using Services.Contracts.DTOs;
using System.ServiceModel;

namespace Services.Contracts
{
    /// <summary>
    /// Provides authentication operations for logging users into the application.
    /// </summary>
    [ServiceContract]
    public interface ILoginManager
    {
        /// <summary>
        /// Attempts to log in a user with the provided credentials.
        /// </summary>
        /// <param name="email">The user email.</param>
        /// <param name="password">The plaintext password to authenticate.</param>
        /// <returns>A <see cref="UserDTO"/> representing the authenticated user.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        UserDTO Login(string email, string password);

    }
}
