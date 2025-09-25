using Data;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity.Core.EntityClient;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Host
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            try
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

                using (var ctx = new PasswordLISEntities(entityBuilder.ToString()))
                {

                    Console.WriteLine("Conexión establecida con éxito.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al establecer la conexión: " + ex.Message);
                
            }

        }
    }
}
