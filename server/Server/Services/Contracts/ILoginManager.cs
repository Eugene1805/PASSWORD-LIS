using Services.Contracts.DTOs;
using System.ServiceModel;
using System.Threading.Tasks;

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
        Task<UserDTO> LoginAsync(string email, string password);
        /// <summary>
        /// Checks if the account associated with the given email is verified.
        /// </summary>
        /// <param name="email">The user email.</param>
        /// <returns>True if the account is verified; otherwise, false.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task<bool> IsAccountVerifiedAsync(string email);
        /// <summary>
        /// Sends a verification code to the specified email address.
        /// </summary>
        /// <param name="email">The email address to send the verification code to.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [OperationContract]
        Task SendVerificationCodeAsync(string email);
    }
}
