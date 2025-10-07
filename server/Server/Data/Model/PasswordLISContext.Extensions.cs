
namespace Data.Model
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;

    public partial class PasswordLISEntities
    {
        public PasswordLISEntities(string connectionString)
           : base(connectionString)
        {
        }
    }
}
