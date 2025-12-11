using Data.DAL.Interfaces;
using Data.Model;
using log4net;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Services.Util;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single) ]
    public class TopPlayersManager : ServiceBase, ITopPlayersManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TopPlayersManager));
        private readonly IStatisticsRepository repository;
        public TopPlayersManager(IStatisticsRepository statisticsRepository) :base(log)
        {
            repository = statisticsRepository;
        }
        public async Task<List<TeamDTO>> GetTopAsync(int numberOfTeams)
        {
            return await ExecuteAsync( async () =>
            {
                List<Team> topTeamsFromDb;
                log.InfoFormat("Requesting top {0} teams", numberOfTeams);
                try
                {
                    topTeamsFromDb = await repository.GetTopTeamsAsync(numberOfTeams);
                }
                catch
                {
                    throw FaultExceptionFactory.Create(ServiceErrorCode.StatisticsError,
                        "STATISTICS_ERROR", "An error occurred while fetching statistics. Please try again later.");
                }

                if (topTeamsFromDb == null)
                {
                    return new List<TeamDTO>();
                }

                return MapToTeamDTOs(topTeamsFromDb);
            }, context:"TopPlayersManager: GetTopAsync ");
        }

        private List<TeamDTO> MapToTeamDTOs(List<Team> teams)
        {
            var result = new List<TeamDTO>();

            foreach (var team in teams)
            {
                if (team.Player == null)
                {
                    log.ErrorFormat("Data Integrity Error: Team with ID {0} (Score: {1}) has a NULL Player collection.",
                        team.Id, team.TotalPoints);

                    throw FaultExceptionFactory.Create(ServiceErrorCode.DataIntegrityError,
                        "DATA_INTEGRITY_ERROR", "Server data corruption detected (Team-Player).");
                }

                var nicknames = new List<string>();
                foreach (var player in team.Player)
                {
                    if (player.UserAccount == null)
                    {
                        log.ErrorFormat("Data Integrity Error: Player with ID {0} (in Team {1}) has a NULL UserAccount.",
                            player.Id, team.Id);

                        throw FaultExceptionFactory.Create(ServiceErrorCode.DataIntegrityError,
                            "DATA_INTEGRITY_ERROR", "Server data corruption detected (Player-Account).");
                    }
                    nicknames.Add(player.UserAccount.Nickname);
                }

                result.Add(new TeamDTO
                {
                    Score = team.TotalPoints,
                    PlayersNicknames = nicknames
                });
            }
            return result;
        }
    }
}
