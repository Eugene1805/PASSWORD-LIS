using Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Entity.Core.EntityClient;
using System.Data.SqlClient;


namespace Test.UnitTest
{
    public class DatabaseConnectionTests
    {
        [Fact]
        public void TestDatabaseConnection_WithValidCredentials_ShouldConnectSuccessfully()
        {
            // Arrange
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

            // Act & Assert
            using (var ctx = new PasswordLISEntities(entityBuilder.ToString()))
            {
                Assert.True(ctx.Database.Exists(), "La conexión a la base de datos no se pudo establecer.");
            }
        }
    }
}
