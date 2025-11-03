using Data.DAL.Interfaces;
using Data.Model;
using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single) ]
    public class TopPlayersManager : ITopPlayersManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TopPlayersManager));
        private readonly IStatisticsRepository repository;
        public TopPlayersManager(IStatisticsRepository statisticsRepository)
        {
            repository = statisticsRepository;
        }
        public async Task<List<TeamDTO>> GetTopAsync(int numberOfTeams)
        {
            try
            {
                log.InfoFormat("Requesting top {0} teams", numberOfTeams);
                List<Team> topTeamsFromDb = await repository.GetTopTeamsAsync(numberOfTeams);

                List<TeamDTO> topTeamsDto = topTeamsFromDb.Select(team => new TeamDTO
                {
                    Score = team.TotalPoints,
                    PlayersNicknames = team.Player
                                     .Select(p => p.UserAccount.Nickname)
                                     .ToList()
                }).ToList();
                return topTeamsDto;
            }
            catch (Exception ex)
            {
                log.Error("Error retrieving top teams statistics.", ex);
                var errorDetail = new ServiceErrorDetailDTO
                {
                    Code = ServiceErrorCode.StatisticsError,
                    ErrorCode = "STATISTICS_ERROR",
                    Message = "An error occurred while fetching statistics. Please try again later."
                };

                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message)); 
            }
            
        }
    }
}
