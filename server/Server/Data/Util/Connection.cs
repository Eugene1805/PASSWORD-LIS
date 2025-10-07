using System;
using System.Collections.Generic;
using System.Data.Entity.Core.EntityClient;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Util
{
    public static class Connection
    {
        public static String GetConnectionString()
        {
            var sqlBuilder = new SqlConnectionStringBuilder
            {
                DataSource = Environment.GetEnvironmentVariable("DB_SERVER_PASSWORDLIS") ?? "localhost",
                InitialCatalog = "PasswordLIS",
                UserID = Environment.GetEnvironmentVariable("DB_USER_PASSWORDLIS"),
                Password = Environment.GetEnvironmentVariable("DB_PASS_PASSWORDLIS"),
                MultipleActiveResultSets = true
            };

            var entityBuilder = new EntityConnectionStringBuilder
            {
                Provider = "System.Data.SqlClient",
                ProviderConnectionString = sqlBuilder.ToString(),
                Metadata = "res://*/PasswordLIS.csdl|res://*/PasswordLIS.ssdl|res://*/PasswordLIS.msl"
            };
            return entityBuilder.ToString();
        }
    }
}
