using Data.DAL.Interfaces;
using Data.Model;
using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
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
                log.InfoFormat("Solicitando top {0} equipos",numberOfTeams);
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
            catch (Exception)
            {
                log.ErrorFormat("Error al consultar las estadísticas de los mejores equipos.");
                var errorDetail = new ServiceErrorDetailDTO
                {
                    ErrorCode = "STATISTICS_ERROR",
                    Message = "Ocurrió un error al consultar las estadísticas. Por favor, inténtelo más tarde."
                };

                throw new FaultException<ServiceErrorDetailDTO>(errorDetail, new FaultReason(errorDetail.Message)); 
            }
            
        }
    }
}
