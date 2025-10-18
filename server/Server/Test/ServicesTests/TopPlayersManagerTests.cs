using Data.DAL.Interfaces;
using Data.Model;
using Moq;
using Services.Services;
using Services.Contracts.DTOs;
using System.ServiceModel;

namespace Test.ServicesTests
{
    public class TopPlayersManagerTests
    {
        private readonly Mock<IStatisticsRepository> mockStatisticsRepository;
        private readonly TopPlayersManager topPlayersManager;

        public TopPlayersManagerTests()
        {
            mockStatisticsRepository = new Mock<IStatisticsRepository>();
            topPlayersManager = new TopPlayersManager(mockStatisticsRepository.Object);
        }

        [Fact]
        public async Task GetTop_WhenRepositoryReturnsTeams_ShouldMapToTeamDTOsCorrectly()
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

            // CAMBIO 1: Usar ReturnsAsync
            mockStatisticsRepository.Setup(repo => repo.GetTopTeamsAsync(numberOfTeams)).ReturnsAsync(teamsFromDb);

            // Act
            // CAMBIO 2: Usar await
            var result = await topPlayersManager.GetTopAsync(numberOfTeams);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);

            Assert.Equal(100, result[0].Score);
            Assert.Equal(2, result[0].PlayersNicknames.Count);
            Assert.Contains("Player1", result[0].PlayersNicknames);
            Assert.Contains("Player2", result[0].PlayersNicknames);

            Assert.Equal(90, result[1].Score);
            Assert.Single(result[1].PlayersNicknames);
            Assert.Equal("Player3", result[1].PlayersNicknames.First());
        }

        [Fact]
        public async Task GetTop_WhenRepositoryReturnsEmptyList_ShouldReturnEmptyList()
        {
            // Arrange
            var numberOfTeams = 5;
            mockStatisticsRepository.Setup(repo => repo.GetTopTeamsAsync(numberOfTeams)).ReturnsAsync(new List<Team>());

            // Act
            var result = await topPlayersManager.GetTopAsync(numberOfTeams);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetTop_WhenTeamHasNoPlayers_ShouldMapToEmptyNicknameList()
        {
            // Arrange
            var numberOfTeams = 1;
            var teamsFromDb = new List<Team>
            {
                new Team
                {
                    TotalPoints = 50,
                    Player = new List<Player>()
                }
            };
            mockStatisticsRepository.Setup(repo => repo.GetTopTeamsAsync(numberOfTeams)).ReturnsAsync(teamsFromDb);

            // Act
            var result = await topPlayersManager.GetTopAsync(numberOfTeams);

            // Assert
            Assert.Single(result);
            Assert.Equal(50, result[0].Score);
            Assert.Empty(result[0].PlayersNicknames);
        }

        [Fact]
        public async Task GetTop_WhenRepositoryThrowsException_ShouldThrowFaultException()
        {
            // Arrange
            var numberOfTeams = 3;
            mockStatisticsRepository.Setup(repo => repo.GetTopTeamsAsync(numberOfTeams))
                                     .ThrowsAsync(new System.Exception("Error de base de datos simulado"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => topPlayersManager.GetTopAsync(numberOfTeams)
            );

            // (Opcional) Verificar que el código de error sea el correcto
            Assert.Equal("STATISTICS_ERROR", exception.Detail.ErrorCode);
        }
    }
}