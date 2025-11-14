using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Core.EntityClient;

namespace Data.Model
{
    // Partial extension to allow constructing context with an external DbConnection (Effort in-memory provider)
    public partial class PasswordLISEntities : DbContext
    {
        public PasswordLISEntities(DbConnection connection) : base(connection, contextOwnsConnection: false)
        {
            // Configuraciones importantes para Effort
            Configuration.LazyLoadingEnabled = false;
            Configuration.ProxyCreationEnabled = false;
            Database.SetInitializer<PasswordLISEntities>(null);
        }

        // Constructor para EFFORT con configuración específica
        public PasswordLISEntities(DbConnection connection, bool contextOwnsConnection) : base(connection, contextOwnsConnection)
        {
            Configuration.LazyLoadingEnabled = false;
            Configuration.ProxyCreationEnabled = false;
            Database.SetInitializer<PasswordLISEntities>(null);
        }

    }
}
