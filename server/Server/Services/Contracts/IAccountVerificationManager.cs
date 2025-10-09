using Services.Contracts.DTOs;
using System.ServiceModel;

namespace Services.Contracts
{
    [ServiceContract]
    public interface IAccountVerificationManager
    {
        [OperationContract]
        bool VerifyEmail(EmailVerificationDTO emailVerificationDTO);
        [OperationContract]
        bool ResendVerificationCode(string email);
    }
}
