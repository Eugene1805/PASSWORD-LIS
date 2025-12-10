using System.Linq;
using System.Threading.Tasks;
using Data.DAL.Implementations;
using Data.Model;
using Xunit;

namespace TestsEF.DataTests
{
    public class PlayerRepositoryTests : DataTestsBase
    {
        private readonly PlayerRepository repository;

        public PlayerRepositoryTests()
        {
            repository = CreatePlayerRepository();
            SeedUsersAndPlayers();
        }

        private void SeedUsersAndPlayers()
        {
            for (int i = 1; i <= 3; i++)
            {
                var ua = CreateValidUser($"user{i}@test.com", $"User{i}");
                ua.EmailVerified = true;
                var player = new Player
                {
                    UserAccount = ua,
                    TotalPoints = i * 10
                };
                Context.UserAccount.Add(ua);
                Context.Player.Add(player);
            }
            Context.SaveChanges();
        }

        [Fact]
        public async Task GetPlayerByEmailAsync_WhenEmailExists_ReturnsPlayerWithUserAccount()
        {
            var player = await repository.GetPlayerByEmailAsync("user2@test.com");

            Assert.NotNull(player);
            Assert.True(player.Id > 0);
            Assert.NotNull(player.UserAccount);
            Assert.Equal("user2@test.com", player.UserAccount.Email);
            Assert.Equal(20, player.TotalPoints);
        }

        [Fact]
        public async Task GetPlayerByEmailAsync_WhenEmailDoesNotExist_ReturnsSentinelPlayer()
        {
            var player = await repository.GetPlayerByEmailAsync("nope@test.com");

            Assert.NotNull(player);
            Assert.Equal(-1, player.Id);
        }

        [Fact]
        public async Task GetPlayerByIdAsync_WhenIdExists_ReturnsPlayerWithUserAccount()
        {
            int existingPlayerId;
            using (var ctx = NewContext())
            {
                existingPlayerId = ctx.Player.First().Id;
            }

            var player = await repository.GetPlayerByIdAsync(existingPlayerId);

            Assert.NotNull(player);
            Assert.Equal(existingPlayerId, player.Id);
            Assert.NotNull(player.UserAccount);
            Assert.StartsWith("user", player.UserAccount.Email);
        }

        [Fact]
        public async Task GetPlayerByIdAsync_WhenIdDoesNotExist_ReturnsSentinelPlayer()
        {
            var player = await repository.GetPlayerByIdAsync(9999);

            Assert.NotNull(player);
            Assert.Equal(-1, player.Id);
        }

        [Fact]
        public async Task UpdatePlayerTotalPointsAsync_WhenPlayerExists_IncrementsTotalPoints()
        {
            int targetId;
            int originalPoints;
            using (var ctx = NewContext())
            {
                var p = ctx.Player.First();
                targetId = p.Id;
                originalPoints = p.TotalPoints;
            }

            await repository.UpdatePlayerTotalPointsAsync(targetId, 15);

            using (var verifyCtx = NewContext())
            {
                var updated = verifyCtx.Player.Find(targetId);
                Assert.NotNull(updated);
                Assert.Equal(originalPoints + 15, updated.TotalPoints);
            }
        }

        [Fact]
        public async Task UpdatePlayerTotalPointsAsync_WhenPlayerMissing_DoesNothing()
        {
            await repository.UpdatePlayerTotalPointsAsync(5555, 10);

            using (var verifyCtx = NewContext())
            {
                Assert.Null(verifyCtx.Player.Find(5555));
                var player3 = verifyCtx.Player.Include("UserAccount")
                    .First(p => p.UserAccount.Email == "user3@test.com");
                Assert.Equal(30, player3.TotalPoints);
            }
        }
    }
}
