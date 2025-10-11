using Data.DAL.Interfaces;
using Data.Model;
using Services.Contracts;
using Services.Contracts.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single) ]
    public class TopPlayersManager : ITopPlayersManager
    {
        private readonly IStatisticsRepository repository;
        public TopPlayersManager(IStatisticsRepository statisticsRepository)
        {
            repository = statisticsRepository;
        }
        public List<TeamDTO> GetTop(int numberOfTeams)
        {
            List<Team> topTeamsFromDb = repository.GetTopTeams(numberOfTeams);

            List<TeamDTO> topTeamsDto = topTeamsFromDb.Select(team => new TeamDTO
            {
                Score = team.TotalPoints,
                PlayersNicknames = team.Player
                                 .Select(p => p.UserAccount.Nickname)
                                 .ToList()
            }).ToList();
            Console.WriteLine("Lista de jugadores:");
            foreach (var item in topTeamsDto)
            {
                Console.WriteLine(item.PlayersNicknames.Count);
            }
            return topTeamsDto;
        }
    }
}
