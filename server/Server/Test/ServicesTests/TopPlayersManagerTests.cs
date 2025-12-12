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
            var numberOfTeams = 2;
            var teamsFromDb = new List<Team>
            {
                new Team
                {
                    TotalPoints = 100,
                    Player = new List<Player>
                    {
                        new Player 
                        { 
                            UserAccount = new UserAccount 
                            { 
                                Nickname = "Player1" 
                            } 
                        },
                        new Player 
                        { 
                            UserAccount = new UserAccount 
                            { 
                                Nickname = "Player2" 
                            } 
                        }
                    }
                },
                new Team
                {
                    TotalPoints = 90,
                    Player = new List<Player>
                    {
                        new Player 
                        { 
                            UserAccount = new UserAccount 
                            { 
                                Nickname = "Player3" 
                            } 
                        }
                    }
                }
            };

            mockStatisticsRepository.Setup(repo => repo.GetTopTeamsAsync(numberOfTeams)).ReturnsAsync(teamsFromDb);

            var result = await topPlayersManager.GetTopAsync(numberOfTeams);

            var expected = (
                NotNull: true,
                Count: 2,
                FirstScore: 100,
                FirstPlayersCount: 2,
                HasP1: true,
                HasP2: true,
                SecondScore: 90,
                SecondPlayersCount: 1,
                SecondPlayer: (string?)"Player3"
            );
            var actual = (
                NotNull: result != null,
                Count: result?.Count ?? 0,
                FirstScore: result?.ElementAtOrDefault(0)?.Score ?? -1,
                FirstPlayersCount: result?.ElementAtOrDefault(0)?.PlayersNicknames.Count ?? -1,
                HasP1: result?.ElementAtOrDefault(0)?.PlayersNicknames.Contains("Player1") ?? false,
                HasP2: result?.ElementAtOrDefault(0)?.PlayersNicknames.Contains("Player2") ?? false,
                SecondScore: result?.ElementAtOrDefault(1)?.Score ?? -1,
                SecondPlayersCount: result?.ElementAtOrDefault(1)?.PlayersNicknames.Count ?? -1,
                SecondPlayer: result?.ElementAtOrDefault(1)?.PlayersNicknames.FirstOrDefault()
            );
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetTop_WhenRepositoryReturnsEmptyList_ShouldReturnEmptyList()
        {
            var numberOfTeams = 5;
            mockStatisticsRepository.Setup(repo => repo.GetTopTeamsAsync(numberOfTeams)).ReturnsAsync(new List<Team>());

            var result = await topPlayersManager.GetTopAsync(numberOfTeams);

            Assert.Equal((NotNull: true, Empty: true, Count: 0), (NotNull: result != null, Empty: result!.Count == 0,
                Count: result.Count));
        }

        [Fact]
        public async Task GetTop_WhenTeamHasNoPlayers_ShouldMapToEmptyNicknameList()
        {
            var numberOfTeams = 1;
            var teamsFromDb = new List<Team> 
            { 
                new Team 
                { 
                    TotalPoints = 50, Player = new List<Player>()
                } 
            };
            mockStatisticsRepository.Setup(repo => repo.GetTopTeamsAsync(numberOfTeams)).ReturnsAsync(teamsFromDb);

            var result = await topPlayersManager.GetTopAsync(numberOfTeams);

            Assert.Equal((Count: 1, Score: 50, NicknamesCount: 0), (Count: result.Count, Score: result[0].Score, 
                NicknamesCount: result[0].PlayersNicknames.Count));
        }

        [Fact]
        public async Task GetTop_WhenRepositoryThrowsException_ShouldThrowFaultException()
        {
            var numberOfTeams = 3;
            mockStatisticsRepository.Setup(repo => repo.GetTopTeamsAsync(numberOfTeams)).
                ThrowsAsync(new System.Exception("Simulated Database error"));

            FaultException<ServiceErrorDetailDTO>? ex = null;
            try 
            { 
                await topPlayersManager.GetTopAsync(numberOfTeams); 
            } 
            catch (FaultException<ServiceErrorDetailDTO> e) 
            {
                ex = e; 
            }
            Assert.Equal((Threw: true, Code: "STATISTICS_ERROR"), (Threw: ex != null, Code: ex?.Detail.ErrorCode));
        }

        [Fact]
        public async Task GetTop_WhenRepositoryReturnsNull_ShouldReturnEmptyList()
        {
            var numberOfTeams = 2;
            mockStatisticsRepository.Setup(r => r.GetTopTeamsAsync(numberOfTeams)).ReturnsAsync((List<Team>)null!);

            var result = await topPlayersManager.GetTopAsync(numberOfTeams);

            Assert.Equal((NotNull: true, Empty: true, Count: 0), (NotNull: result != null, Empty: result!.Count == 0,
                Count: result.Count));
        }

        [Fact]
        public async Task GetTop_WhenAPlayerHasNullUserAccount_ShouldThrowDataIntegrityFault()
        {
            var numberOfTeams = 1;
            var teams = new List<Team>
            {
                new Team
                {
                    Id = 10,
                    TotalPoints = 12,
                    Player = new List<Player> 
                    {
                        new Player 
                        {
                            Id = 5, UserAccount = null! 
                        } 
                    }
                }
            };

            mockStatisticsRepository.Setup(r => r.GetTopTeamsAsync(numberOfTeams)).ReturnsAsync(teams);

            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => topPlayersManager.GetTopAsync(numberOfTeams)
            );

            Assert.Equal("DATA_INTEGRITY_ERROR", ex.Detail.ErrorCode);
        }

        [Fact]
        public async Task GetTop_WhenTeamPlayerCollectionIsNull_ShouldThrowDataIntegrityFault()
        {
            var numberOfTeams = 1;
            var teams = new List<Team> 
            { 
                new Team 
                { 
                    Id = 20, TotalPoints = 10, Player = null! 
                } 
            };
            mockStatisticsRepository.Setup(r => r.GetTopTeamsAsync(numberOfTeams)).ReturnsAsync(teams);

            var ex = await Assert.ThrowsAsync<FaultException<ServiceErrorDetailDTO>>(
                () => topPlayersManager.GetTopAsync(numberOfTeams)
            );

            Assert.Equal("DATA_INTEGRITY_ERROR", ex.Detail.ErrorCode);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GetTop_WithNonPositiveNumber_ShouldThrowFaultException_WhenRepositoryRejects(int n)
        {
            mockStatisticsRepository.Setup(r => r.GetTopTeamsAsync(n)).
                ThrowsAsync(new ArgumentOutOfRangeException(string.Format("{0}", n)));

            FaultException<ServiceErrorDetailDTO>? ex = null;
            try 
            { 
                await topPlayersManager.GetTopAsync(n); 
            } 
            catch (FaultException<ServiceErrorDetailDTO> e) 
            {
                ex = e; 
            }

            Assert.Equal((Threw: true, Code: "STATISTICS_ERROR"), (Threw: ex != null, Code: ex?.Detail.ErrorCode));
        }

        [Fact]
        public async Task GetTop_WithZero_ShouldReturnEmpty_WhenRepositoryReturnsEmpty()
        {
            mockStatisticsRepository.Setup(r => r.GetTopTeamsAsync(0)).ReturnsAsync(new List<Team>());

            var result = await topPlayersManager.GetTopAsync(0);

            Assert.Equal((NotNull: true, Empty: true, Count: 0), (NotNull: result != null, Empty: result!.Count == 0,
                Count: result.Count));
        }
    }
}