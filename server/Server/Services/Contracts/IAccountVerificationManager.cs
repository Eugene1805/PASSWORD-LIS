using Services.Contracts.DTOs;
using System.ServiceModel;

namespace Services.Contracts
{
    /// <summary>
    /// Defines operations for verifying user accounts via email verification codes.
    /// </summary>
    [ServiceContract]
    public interface IAccountVerificationManager
    {
        /// <summary>
        /// Verifies the specified email address using a previously issued verification code.
        /// </summary>
        /// <param name="emailVerificationDTO">The email and verification code payload.</param>
        /// <returns>True if the email was successfully verified; otherwise, false.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        bool VerifyEmail(EmailVerificationDTO emailVerificationDTO);

        /// <summary>
        /// Generates and sends a new verification code to the specified email address.
        /// </summary>
        /// <param name="email">The target email address.</param>
        /// <returns>True if the code was resent; otherwise, false (e.g., rate limited).</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        bool ResendVerificationCode(string email);
    }
}
