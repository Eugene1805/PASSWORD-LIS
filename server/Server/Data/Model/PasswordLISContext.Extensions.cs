using System.Data.Common;

namespace Data.Model
{
    public partial class PasswordLISEntities
    {
        public PasswordLISEntities(string connectionString) : base(connectionString)
        {
        }
        public PasswordLISEntities(DbConnection existingConnection, bool contextOwnsConnection)
            : base(existingConnection, contextOwnsConnection)
        {
        }
    }
}