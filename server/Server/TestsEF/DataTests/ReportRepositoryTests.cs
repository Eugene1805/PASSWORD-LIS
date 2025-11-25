using System;
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
            var report = new Data.Model.Report
            {
                ReporterPlayerId = 1,
                ReportedPlayerId = 2,
                Reason = "Abusive language"
            };

            await _repository.AddReportAsync(report);

            using (var verifyCtx = NewContext())
            {
                var inDb = verifyCtx.Report.FirstOrDefault(r => r.ReportedPlayerId == 2 && r.ReporterPlayerId == 1);
                Assert.NotNull(inDb);
                Assert.Equal("Abusive language", inDb.Reason);
                Assert.True(inDb.CreatedAt > DateTime.UtcNow.AddMinutes(-5));
            }
        }

        [Fact]
        public async Task GetReportCountForPlayerSinceAsync_ShouldReturnFilteredCount()
        {
            var cutoff = DateTime.UtcNow;
            // older report (simulate by setting CreatedAt manually)
            await _repository.AddReportAsync(new Data.Model.Report
            {
                ReporterPlayerId = 1,
                ReportedPlayerId = 3,
                Reason = "Old",
                CreatedAt = cutoff.AddMinutes(-10)
            });
            // recent reports
            for (int i = 0; i < 2; i++)
            {
                await _repository.AddReportAsync(new Data.Model.Report
                {
                    ReporterPlayerId = 2 + i,
                    ReportedPlayerId = 3,
                    Reason = $"Reason {i}",
                    CreatedAt = cutoff.AddMinutes(1 + i)
                });
            }

            var countSince = await _repository.GetReportCountForPlayerSinceAsync(3, cutoff);
            Assert.Equal(2, countSince);
        }

        [Fact]
        public async Task GetReportCountForPlayerSinceAsync_NoSince_ReturnsAll()
        {
            for (int i = 0; i < 3; i++)
            {
                await _repository.AddReportAsync(new Data.Model.Report
                {
                    ReporterPlayerId = 1,
                    ReportedPlayerId = 4,
                    Reason = $"R{i}"
                });
            }
            var count = await _repository.GetReportCountForPlayerSinceAsync(4, null);
            Assert.Equal(3, count);
        }

        [Fact]
        public async Task HasReporterReportedSinceAsync_ShouldReturnTrue_WhenExistsAfterCutoff()
        {
            var cutoff = DateTime.UtcNow;
            await _repository.AddReportAsync(new Data.Model.Report
            {
                ReporterPlayerId = 1,
                ReportedPlayerId = 5,
                Reason = "Before",
                CreatedAt = cutoff.AddMinutes(-5)
            });
            await _repository.AddReportAsync(new Data.Model.Report
            {
                ReporterPlayerId = 1,
                ReportedPlayerId = 5,
                Reason = "After",
                CreatedAt = cutoff.AddMinutes(2)
            });

            var result = await _repository.HasReporterReportedSinceAsync(1, 5, cutoff);
            Assert.True(result);
        }

        [Fact]
        public async Task HasReporterReportedSinceAsync_ShouldReturnFalse_WhenOnlyBeforeCutoff()
        {
            var cutoff = DateTime.UtcNow;
            await _repository.AddReportAsync(new Data.Model.Report
            {
                ReporterPlayerId = 2,
                ReportedPlayerId = 5,
                Reason = "Before",
                CreatedAt = cutoff.AddMinutes(-2)
            });
            var result = await _repository.HasReporterReportedSinceAsync(2, 5, cutoff);
            Assert.False(result);
        }

        [Fact]
        public async Task AddReportAsync_NullReport_ShouldThrow()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await _repository.AddReportAsync(null));
        }
    }
}
