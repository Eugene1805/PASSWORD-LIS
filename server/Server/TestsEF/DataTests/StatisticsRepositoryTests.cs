using System.Linq;
using System.Threading.Tasks;
using Data.DAL.Implementations;
using Xunit;

namespace TestsEF.DataTests
{
    public class StatisticsRepositoryTests : DataTestsBase
    {
        [Fact]
        public async Task GetTopTeamsAsync_WhenMultipleTeams_ReturnsTopN_InDescendingOrder_WithIncludes()
        {

            SeedTeams((10, 2), (50, 3), (20, 1), (50, 2), (5, 4));
            var repo = CreateStatisticsRepository();

           
            var result = await repo.GetTopTeamsAsync(3);

            
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.True(result[0].TotalPoints >= result[1].TotalPoints);
            Assert.True(result[1].TotalPoints >= result[2].TotalPoints);
            Assert.Equal(50, result[0].TotalPoints);
            Assert.Equal(50, result[1].TotalPoints);
            Assert.Equal(20, result[2].TotalPoints);

            foreach (var team in result)
            {
                Assert.NotNull(team.Player);
                Assert.True(team.Player.Count >= 1);
                foreach (var p in team.Player)
                {
                    Assert.NotNull(p.UserAccount);
                    Assert.False(string.IsNullOrEmpty(p.UserAccount.Email));
                }
            }
        }

        [Fact]
        public async Task GetTopTeamsAsync_WhenRequestExceedsAvailable_ReturnsAll()
        {

            SeedTeams((15, 1), (5, 1));
            var repo = CreateStatisticsRepository();


            var result = await repo.GetTopTeamsAsync(10);


            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal(new[] { 15, 5 }, result.Select(t => t.TotalPoints).ToArray());
        }

        [Fact]
        public async Task GetTopTeamsAsync_RequestZero_ReturnsEmpty()
        {

            SeedTeams((10, 1), (8, 1));
            var repo = CreateStatisticsRepository();


            var result = await repo.GetTopTeamsAsync(0);


            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetTopTeamsAsync_WhenNoTeams_ReturnsEmpty()
        {

            var repo = CreateStatisticsRepository();


            var result = await repo.GetTopTeamsAsync(5);


            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }
}
