using Services.Contracts.DTOs;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Contracts
{
    /// <summary>
    /// Provides operations to query leaderboards and top player teams.
    /// </summary>
    [ServiceContract]
    public interface ITopPlayersManager
    {
        /// <summary>
        /// Gets the top teams by total points.
        /// </summary>
        /// <param name="numberOfTeams">How many teams to return.</param>
        /// <returns>A list of top teams ordered by score.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task<List<TeamDTO>> GetTopAsync(int numberOfTeams);
    }
}
