using System.Linq;
using System.Threading.Tasks;
using Data.DAL.Implementations;
using Xunit;

namespace TestsEF.DataTests
{
    public class ReportRepositoryTests : DataTestsBase
    {
        private readonly ReportRepository _repository;

        public ReportRepositoryTests()
        {
            _repository = CreateReportRepository();
            SeedPlayers(5);
        }

        [Fact]
        public async Task AddReportAsync_ShouldPersistReport_WithAllFields()
        {
            var now = System.DateTime.UtcNow;
            var report = new Data.Model.Report
            {
                ReporterPlayerId = 1,
                ReportedPlayerId = 2,
                Reason = "Abusive language",
                CreatedAt = now
            };

            await _repository.AddReportAsync(report);

            using (var verifyCtx = NewContext())
            {
                var inDb = verifyCtx.Report.FirstOrDefault(r => r.ReportedPlayerId == 2 && r.ReporterPlayerId == 1);
                Assert.NotNull(inDb);
                Assert.Equal("Abusive language", inDb.Reason);
                Assert.Equal(2, inDb.ReportedPlayerId);
                Assert.Equal(1, inDb.ReporterPlayerId);
            }
        }

        [Fact]
        public async Task GetReportCountForPlayerAsync_ShouldReturnCorrectCount_ForSinglePlayer()
        {
            for (int i = 0; i < 3; i++)
            {
                await _repository.AddReportAsync(new Data.Model.Report
                {
                    ReporterPlayerId = 1,
                    ReportedPlayerId = 3,
                    Reason = $"Reason {i}",
                    CreatedAt = System.DateTime.UtcNow
                });
            }

            var count = await _repository.GetReportCountForPlayerAsync(3);

            Assert.Equal(3, count);
        }

        [Fact]
        public async Task GetReportCountForPlayerAsync_ShouldReturnZero_WhenNoReports()
        {
            var count = await _repository.GetReportCountForPlayerAsync(4);
            Assert.Equal(0, count);
        }

        [Fact]
        public async Task AddReportAsync_MultiplePlayers_ShouldMaintainSeparateCounts()
        {
            await _repository.AddReportAsync(new Data.Model.Report { ReporterPlayerId = 1, ReportedPlayerId = 2, Reason = "r1", CreatedAt = System.DateTime.UtcNow });
            await _repository.AddReportAsync(new Data.Model.Report { ReporterPlayerId = 1, ReportedPlayerId = 3, Reason = "r2", CreatedAt = System.DateTime.UtcNow });
            await _repository.AddReportAsync(new Data.Model.Report { ReporterPlayerId = 2, ReportedPlayerId = 3, Reason = "r3", CreatedAt = System.DateTime.UtcNow });
            for (int i = 0; i < 4; i++)
            {
                await _repository.AddReportAsync(new Data.Model.Report { ReporterPlayerId = 4, ReportedPlayerId = 5, Reason = $"rp{i}", CreatedAt = System.DateTime.UtcNow });
            }

            var c2 = await _repository.GetReportCountForPlayerAsync(2);
            var c3 = await _repository.GetReportCountForPlayerAsync(3);
            var c5 = await _repository.GetReportCountForPlayerAsync(5);

            Assert.Equal(1, c2);
            Assert.Equal(2, c3);
            Assert.Equal(4, c5);
        }

        [Fact]
        public async Task AddReportAsync_NullReport_ShouldThrow()
        {
            await Assert.ThrowsAsync<System.ArgumentNullException>(async () => await _repository.AddReportAsync(null));
        }
    }
}
