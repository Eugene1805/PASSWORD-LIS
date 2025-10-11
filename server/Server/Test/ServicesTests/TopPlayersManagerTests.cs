using Data.DAL.Interfaces;
using Data.Model;
using Moq;
using Services.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.ServicesTests
{
    public class TopPlayersManagerTests
    {
        private readonly Mock<IStatisticsRepository> _mockStatisticsRepository;
        private readonly TopPlayersManager _topPlayersManager;

        public TopPlayersManagerTests()
        {
            // Arrange (Configuración común)
            _mockStatisticsRepository = new Mock<IStatisticsRepository>();
            _topPlayersManager = new TopPlayersManager(_mockStatisticsRepository.Object);
        }

        [Fact]
        public void GetTop_WhenRepositoryReturnsTeams_ShouldMapToTeamDTOsCorrectly()
        {
            // Arrange
            var numberOfTeams = 2;
            var teamsFromDb = new List<Team>
        {
            new Team
            {
                TotalPoints = 100,
                Player = new List<Player>
                {
                    new Player { UserAccount = new UserAccount { Nickname = "Player1" } },
                    new Player { UserAccount = new UserAccount { Nickname = "Player2" } }
                }
            },
            new Team
            {
                TotalPoints = 90,
                Player = new List<Player>
                {
                    new Player { UserAccount = new UserAccount { Nickname = "Player3" } }
                }
            }
        };

            // Configuramos el mock para que devuelva nuestra lista de prueba
            _mockStatisticsRepository.Setup(repo => repo.GetTopTeams(numberOfTeams)).Returns(teamsFromDb);

            // Act
            var result = _topPlayersManager.GetTop(numberOfTeams);

            // Assert
            // Verificamos que se llamó al repositorio
            _mockStatisticsRepository.Verify(repo => repo.GetTopTeams(numberOfTeams), Times.Once);

            // Verificamos que el resultado no sea nulo y tenga la cantidad correcta de equipos
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);

            // Verificamos que el mapeo del primer equipo sea correcto
            Assert.Equal(100, result[0].Score);
            Assert.Equal(2, result[0].PlayersNicknames.Count);
            Assert.Contains("Player1", result[0].PlayersNicknames);
            Assert.Contains("Player2", result[0].PlayersNicknames);

            // Verificamos el mapeo del segundo equipo
            Assert.Equal(90, result[1].Score);
            Assert.Single(result[1].PlayersNicknames); // Verifica que solo hay un elemento
            Assert.Equal("Player3", result[1].PlayersNicknames.First());
        }

        [Fact]
        public void GetTop_WhenRepositoryReturnsEmptyList_ShouldReturnEmptyList()
        {
            // Arrange
            var numberOfTeams = 5;
            // Configuramos el mock para que devuelva una lista vacía
            _mockStatisticsRepository.Setup(repo => repo.GetTopTeams(numberOfTeams)).Returns(new List<Team>());

            // Act
            var result = _topPlayersManager.GetTop(numberOfTeams);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void GetTop_WhenTeamHasNoPlayers_ShouldMapToEmptyNicknameList()
        {
            // Arrange
            var numberOfTeams = 1;
            var teamsFromDb = new List<Team>
        {
            new Team
            {
                TotalPoints = 50,
                Player = new List<Player>() // Lista de jugadores vacía
            }
        };

            _mockStatisticsRepository.Setup(repo => repo.GetTopTeams(numberOfTeams)).Returns(teamsFromDb);

            // Act
            var result = _topPlayersManager.GetTop(numberOfTeams);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(50, result[0].Score);
            Assert.Empty(result[0].PlayersNicknames); // La lista de nicknames debe estar vacía
        }
    }
}
