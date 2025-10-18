using Services.Contracts.DTOs;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Contracts
{
    [ServiceContract]
    public interface ITopPlayersManager
    {
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task<List<TeamDTO>> GetTopAsync(int numberOfTeams);
    }
}
