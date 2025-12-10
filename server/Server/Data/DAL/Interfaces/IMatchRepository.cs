using System.Threading.Tasks;

namespace Data.DAL.Interfaces
{
    public interface IMatchRepository
    {
        Task SaveMatchResultAsync(MatchResultData matchResultData);
    }
}
