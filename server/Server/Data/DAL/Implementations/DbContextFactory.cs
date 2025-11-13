using Data.DAL.Interfaces;
using Data.Model;
using Data.Util;

namespace Data.DAL.Implementations
{
    public class DbContextFactory : IDbContextFactory
    {
        public PasswordLISEntities CreateDbContext()
        {
            return new PasswordLISEntities(Connection.GetConnectionString());
        }
    }
}
