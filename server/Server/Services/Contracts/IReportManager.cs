using Services.Contracts.DTOs;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Contracts
{
    [ServiceContract]
    public interface IReportManager
    {
        [OperationContract]
        Task<bool> SubmitReportAsync(ReportDTO reportDTO);
    }
}
