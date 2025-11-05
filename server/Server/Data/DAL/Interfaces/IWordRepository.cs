using System.Collections.Generic;
using System.Threading.Tasks;

namespace Data.DAL.Interfaces
{
    public interface IWordRepository
    {
        Task<List<string>> GetRandomWordsAsync(int count);
    }
}
