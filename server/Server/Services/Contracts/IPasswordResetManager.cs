using Services.Contracts.DTOs;
using System.ServiceModel;

namespace Services.Contracts
{
    /// <summary>
    /// Provides operations related to secure password reset flows.
    /// </summary>
    [ServiceContract]
    public interface IPasswordResetManager
    {
        /// <summary>
        /// Resets the password for a user when a valid reset code is provided.
        /// </summary>
        /// <param name="passwordResetDTO">The email, code, and new password.</param>
        /// <returns>True if the password was reset; otherwise, false.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        bool ResetPassword(PasswordResetDTO passwordResetDTO);

        /// <summary>
        /// Requests a password reset code to be sent to the user's email.
        /// </summary>
        /// <param name="emailVerificationDTO">The target email.</param>
        /// <returns>True if a code was sent; otherwise, false.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        bool RequestPasswordResetCode(EmailVerificationDTO emailVerificationDTO);

        /// <summary>
        /// Validates whether a provided password reset code is correct and still valid.
        /// </summary>
        /// <param name="emailVerificationDTO">The email and code to validate.</param>
        /// <returns>True if the code is valid; otherwise, false.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        bool ValidatePasswordResetCode(EmailVerificationDTO emailVerificationDTO);
    }
}
