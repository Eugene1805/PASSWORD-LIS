using System;
using System.Data.Entity.Core.EntityClient;
using System.Data.SqlClient;

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
                Metadata = "res://*/Model.PasswordLISModel.csdl|res://*/Model.PasswordLISModel.ssdl|res://*/Model.PasswordLISModel.msl"

            };
            return entityBuilder.ToString();
        }
    }
}
