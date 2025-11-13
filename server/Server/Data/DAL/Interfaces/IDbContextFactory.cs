using Data.Model;

namespace Data.DAL.Interfaces
{
    public interface IDbContextFactory
    {
        PasswordLISEntities CreateDbContext();
    }
}
