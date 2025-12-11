using System.Data.Common;

namespace Data.Model
{
    public partial class PasswordLISEntities
    {
        public PasswordLISEntities(string ConnectionString) : base(ConnectionString)
        {
        }
        public PasswordLISEntities(DbConnection ExistingConnection, bool ContextOwnsConnection)
            : base(ExistingConnection, ContextOwnsConnection)
        {
        }
    }
}