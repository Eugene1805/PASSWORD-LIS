using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.DAL.Implementations;
using Xunit;

namespace TestsEF.DataTests
{
    public class WordRepositoryTests : DataTestsBase
    {
        private WordRepository CreateRepository()
        {
            return CreateWordRepository();
        }

        [Fact]
        public async Task GetRandomWordsAsync_WhenDbHasMany_ReturnsExactCountDistinct()
        {
            SeedWords(10);
            var repo = CreateRepository();


            var result = await repo.GetRandomWordsAsync(5);


            Assert.NotNull(result);
            Assert.Equal(5, result.Count);
            Assert.Equal(5, result.Select(w => w.EnglishWord).Distinct().Count());
            Assert.All(result, w => Assert.StartsWith("W", w.EnglishWord));
        }

        [Fact]
        public async Task GetRandomWordsAsync_WithZeroCount_ReturnsEmpty()
        {

            SeedWords(5);
            var repo = CreateRepository();


            var result = await repo.GetRandomWordsAsync(0);


            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetRandomWordsAsync_WhenDbHasLessThanRequested_ReturnsAllAvailable()
        {

            SeedWords(3);
            var repo = CreateRepository();


            var result = await repo.GetRandomWordsAsync(5);


            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            var expected = new HashSet<string>(new[] 
            { 
                "W1",
                "W2",
                "W3" 
            });
            Assert.All(result, w => Assert.Contains(w.EnglishWord, expected));
        }

        [Fact]
        public async Task GetRandomWordsAsync_WhenDbEmpty_ReturnsEmpty()
        {

            var repo = CreateRepository();


            var result = await repo.GetRandomWordsAsync(5);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetRandomWordsAsync_RequestAll_ReturnsAll()
        {

            SeedWords(7);
            var repo = CreateRepository();


            var result = await repo.GetRandomWordsAsync(7);


            Assert.NotNull(result);
            Assert.Equal(7, result.Count);
            var set = new HashSet<string>(result.Select(w => w.EnglishWord));
            for (int i = 1; i <= 7; i++)
            {
                Assert.Contains("W" + i, set);
            }
        }
    }
}
