using Services.Contracts.DTOs;
using System.ServiceModel;

namespace Services.Contracts
{
    [ServiceContract]
    public interface IPasswordResetManager
    {
        [OperationContract]
        bool ResetPassword(PasswordResetDTO passwordResetDTO);
        [OperationContract]
        bool RequestPasswordResetCode(EmailVerificationDTO emailVerificationDTO);
        [OperationContract]
        bool ValidatePasswordResetCode(EmailVerificationDTO emailVerificationDTO);
    }
}
