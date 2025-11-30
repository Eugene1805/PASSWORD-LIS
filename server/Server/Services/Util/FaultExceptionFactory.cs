using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using System.ServiceModel;

namespace Services.Util
{
    public static class FaultExceptionFactory
    {
        public static FaultException<ServiceErrorDetailDTO> Create(ServiceErrorCode code,
            string errorCode, string message)
        {
            var detail = new ServiceErrorDetailDTO
            {
                Code = code,
                ErrorCode = errorCode,
                Message = message
            };
            return new FaultException<ServiceErrorDetailDTO>(detail, new FaultReason(detail.Message));
        }
    }
}
