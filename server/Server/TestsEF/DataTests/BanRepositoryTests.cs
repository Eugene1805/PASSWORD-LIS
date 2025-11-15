using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Data.DAL.Implementations;
using Data.Model;
using Xunit;

namespace TestsEF.DataTests
{
    public class BanRepositoryTests : DataTestsBase
    {
        private readonly BanRepository _repository;

        public BanRepositoryTests()
        {
            _repository = CreateRepository(factory => new BanRepository(factory));
            SeedPlayers(3, emailPrefix: "player", nicknamePrefix: "Player", pointsSelector: i => i * 5, emailVerified: true);
        }

        [Fact]
        public async Task AddBanAsync_PersistsBan()
        {
            var playerId = GetFirstPlayerId();
            var start = DateTime.UtcNow;
            var end = start.AddHours(1);
            var ban = new Ban { PlayerId = playerId, StartTime = start, EndTime = end };

            await _repository.AddBanAsync(ban);

            using (var verifyCtx = new PasswordLISEntities(Connection, false))
            {
                var stored = verifyCtx.Ban.FirstOrDefault(b => b.PlayerId == playerId && b.EndTime == end);
                Assert.NotNull(stored);
                Assert.True(stored.Id > 0);
                Assert.Equal(start, stored.StartTime);
                Assert.Equal(end, stored.EndTime);
            }
        }

        [Fact]
        public async Task GetActiveBanForPlayerAsync_WhenActiveBanExists_ReturnsBan()
        {
            var playerId = GetFirstPlayerId();
            var ban = new Ban
            {
                PlayerId = playerId,
                StartTime = DateTime.UtcNow.AddMinutes(-5),
                EndTime = DateTime.UtcNow.AddMinutes(30)
            };
            await _repository.AddBanAsync(ban);

            var active = await _repository.GetActiveBanForPlayerAsync(playerId);

            Assert.NotNull(active);
            Assert.Equal(playerId, active.PlayerId);
            Assert.True(active.EndTime > DateTime.UtcNow);
        }

        [Fact]
        public async Task GetActiveBanForPlayerAsync_WhenBanExpired_ReturnsNull()
        {
            var playerId = GetFirstPlayerId();
            var ban = new Ban
            {
                PlayerId = playerId,
                StartTime = DateTime.UtcNow.AddHours(-2),
                EndTime = DateTime.UtcNow.AddHours(-1)
            };
            await _repository.AddBanAsync(ban);

            var active = await _repository.GetActiveBanForPlayerAsync(playerId);
            Assert.Null(active);
        }

        [Fact]
        public async Task GetActiveBanForPlayerAsync_WhenNoBan_ReturnsNull()
        {
            int playerId;
            using (var ctx = new PasswordLISEntities(Connection, false))
            {
                playerId = ctx.Player.OrderBy(p => p.Id).Skip(1).First().Id;
            }

            var active = await _repository.GetActiveBanForPlayerAsync(playerId);
            Assert.Null(active);
        }
    }
}
