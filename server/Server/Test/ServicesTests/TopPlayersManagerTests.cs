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

            mockStatisticsRepository.Setup(repo => repo.GetTopTeamsAsync(numberOfTeams)).ReturnsAsync(teamsFromDb);

            // Act
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
            Assert.Equal("Player3", result[1].PlayersNicknames[0]);
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
                                     .ThrowsAsync(new System.Exception("Simulated Database error"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => topPlayersManager.GetTopAsync(numberOfTeams)
            );

            Assert.Equal("STATISTICS_ERROR", exception.Detail.ErrorCode);
        }

        // New tests to amplify coverage for fatal/edge scenarios

        [Fact]
        public async Task GetTop_WhenRepositoryReturnsNull_ShouldThrowFaultException()
        {
            // Arrange
            var numberOfTeams = 2;
            mockStatisticsRepository.Setup(r => r.GetTopTeamsAsync(numberOfTeams))
                                     .ReturnsAsync((List<Team>)null!);

            // Act
            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => topPlayersManager.GetTopAsync(numberOfTeams));

            // Assert - ServiceBase catches ArgumentNullException and converts to NULL_ARGUMENT
            Assert.Equal("NULL_ARGUMENT", ex.Detail.ErrorCode);
        }

        [Fact]
        public async Task GetTop_WhenTeamPlayerCollectionIsNull_ShouldThrowFaultException()
        {
            // Arrange
            var numberOfTeams = 1;
            var teams = new List<Team>
            {
                new Team { TotalPoints = 10, Player = null! }
            };
            mockStatisticsRepository.Setup(r => r.GetTopTeamsAsync(numberOfTeams))
                                     .ReturnsAsync(teams);

            // Act
            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => topPlayersManager.GetTopAsync(numberOfTeams));

            // Assert - ServiceBase catches ArgumentNullException from LINQ and converts to NULL_ARGUMENT
            Assert.Equal("NULL_ARGUMENT", ex.Detail.ErrorCode);
        }

        [Fact]
        public async Task GetTop_WhenAPlayerHasNullUserAccount_ShouldThrowFaultException()
        {
            // Arrange
            var numberOfTeams = 1;
            var teams = new List<Team>
            {
                new Team
                {
                    TotalPoints = 12,
                    Player = new List<Player> { new Player { UserAccount = null! } }
                }
            };
            mockStatisticsRepository.Setup(r => r.GetTopTeamsAsync(numberOfTeams))
                                     .ReturnsAsync(teams);

            // Act
            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => topPlayersManager.GetTopAsync(numberOfTeams));

            // Assert - ServiceBase catches NullReferenceException and converts to UNEXPECTED_ERROR
            Assert.Equal("UNEXPECTED_ERROR", ex.Detail.ErrorCode);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GetTop_WithNonPositiveNumber_ShouldThrowFaultException_WhenRepositoryRejects(int n)
        {
            // Arrange: simulate repository rejecting invalid number via exception
            mockStatisticsRepository.Setup(r => r.GetTopTeamsAsync(n))
                                     .ThrowsAsync(new ArgumentOutOfRangeException(String.Format("{0}",n)));

            // Act
            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => topPlayersManager.GetTopAsync(n));

            // Assert
            Assert.Equal("STATISTICS_ERROR", ex.Detail.ErrorCode);
        }

        [Fact]
        public async Task GetTop_WithZero_ShouldReturnEmpty_WhenRepositoryReturnsEmpty()
        {
            // Arrange: some repositories may decide to return empty for zero
            mockStatisticsRepository.Setup(r => r.GetTopTeamsAsync(0))
                                     .ReturnsAsync(new List<Team>());

            // Act
            var result = await topPlayersManager.GetTopAsync(0);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }
}