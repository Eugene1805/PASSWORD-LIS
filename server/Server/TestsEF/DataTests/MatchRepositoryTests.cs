using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.DAL.Implementations;
using Xunit;

namespace TestsEF.DataTests
{
    public class MatchRepositoryTests : DataTestsBase
    {
        private readonly MatchRepository _repository;

        public MatchRepositoryTests()
        {
            _repository = CreateMatchRepository();
            SeedPlayers(6, emailPrefix: "player", nicknamePrefix: "Player", pointsSelector: i => i * 5, emailVerified: true);
        }

        [Fact]
        public async Task SaveMatchResultAsync_ValidInputs_PersistsMatchTeamsAndRelations()
        {
            // Arrange
            var redIds = GetFirstPlayerIds(3);
            List<int> blueIds;
            using (var ctx = NewContext())
            {
                blueIds = ctx.Player.OrderBy(p => p.Id).Skip(3).Take(2).Select(p => p.Id).ToList();
            }

            // Act
            await _repository.SaveMatchResultAsync(10, 7, redIds, blueIds);

            // Assert
            using (var verifyCtx = NewContext())
            {
                var matches = verifyCtx.Match.Include("Team.Player").ToList();
                Assert.Single(matches);
                var match = matches.First();
                Assert.Equal(2, match.Team.Count);
                var redTeam = match.Team.First(t => t.TotalPoints == 10);
                var blueTeam = match.Team.First(t => t.TotalPoints == 7);
                Assert.Equal(redIds.Count, redTeam.Player.Count);
                Assert.Equal(blueIds.Count, blueTeam.Player.Count);
                Assert.All(redIds, id => Assert.Contains(redTeam.Player, p => p.Id == id));
                Assert.All(blueIds, id => Assert.Contains(blueTeam.Player, p => p.Id == id));
            }
        }

        [Fact]
        public async Task SaveMatchResultAsync_NullRedIds_ThrowsArgumentNullException()
        {
            var blueIds = GetFirstPlayerIds(2);
            await Assert.ThrowsAsync<System.ArgumentNullException>(() => _repository.SaveMatchResultAsync(5, 5, null, blueIds));
        }

        [Fact]
        public async Task SaveMatchResultAsync_NullBlueIds_ThrowsArgumentNullException()
        {
            var redIds = GetFirstPlayerIds(2);
            await Assert.ThrowsAsync<System.ArgumentNullException>(() => _repository.SaveMatchResultAsync(5, 5, redIds, null));
        }

        [Fact]
        public async Task SaveMatchResultAsync_MissingPlayerId_ThrowsAndRollsBack()
        {
            var redIds = GetFirstPlayerIds(2);
            var blueIds = GetFirstPlayerIds(2);
            blueIds[1] = 9999;
            int initialMatchCount;
            int initialTeamCount;
            using (var ctx = NewContext())
            {
                initialMatchCount = ctx.Match.Count();
                initialTeamCount = ctx.Team.Count();
            }

            var ex = await Assert.ThrowsAsync<System.InvalidOperationException>(() => _repository.SaveMatchResultAsync(3, 4, redIds, blueIds));
            Assert.Contains("9999", ex.Message);

            using (var verifyCtx = NewContext())
            {
                Assert.Equal(initialMatchCount, verifyCtx.Match.Count());
                Assert.Equal(initialTeamCount, verifyCtx.Team.Count());
            }
        }

        [Fact]
        public async Task SaveMatchResultAsync_DuplicatePlayerAcrossTeams_AllowsAndPersistsPlayerInBothTeams()
        {
            List<int> playerIds;
            using (var ctx = NewContext())
            {
                playerIds = ctx.Player.OrderBy(p => p.Id).Take(4).Select(p => p.Id).ToList();
            }
            var shared = playerIds[0];
            var redIds = new List<int> { shared, playerIds[1] };
            var blueIds = new List<int> { shared, playerIds[2], playerIds[3] };

            await _repository.SaveMatchResultAsync(8, 9, redIds, blueIds);

            using (var verifyCtx = NewContext())
            {
                var match = verifyCtx.Match.Include("Team.Player").Single();
                var redTeam = match.Team.First(t => t.TotalPoints == 8);
                var blueTeam = match.Team.First(t => t.TotalPoints == 9);
                Assert.Contains(redTeam.Player, p => p.Id == shared);
                Assert.Contains(blueTeam.Player, p => p.Id == shared);
            }
        }
    }
}
