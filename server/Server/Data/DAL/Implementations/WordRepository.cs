using Data.DAL.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Data.DAL.Implementations
{
    public class WordRepository : IWordRepository
    {
        public Task<List<string>> GetRandomWordsAsync(int count)
        {
            return new Task<List<string>>(() =>
            {
                List<string> list = new List<string>();
                list.Add("example1");
                list.Add("example2");
                list.Add("example3");
                list.Add("example4");
                list.Add("example5");
                return list;
            });
        }
    }
}
