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
    public interface ITopPlayersManager
    {
        [OperationContract]
        List<TeamDTO> GetTop(int numberOfTeams);
    }
}
