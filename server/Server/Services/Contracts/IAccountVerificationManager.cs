using Services.Contracts.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

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
