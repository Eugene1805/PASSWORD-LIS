using System;
using System.ServiceModel;
using System.ServiceModel.Description; // Necesario para los metadatos
using Services.Contracts;
using Services.Services;

namespace Host
{
    class Program
    {
        static void Main(string[] args)
        {
            var accountManagerHost = new ServiceHost(typeof(AccountManager));
            var loginManagerHost = new ServiceHost(typeof(LoginManager));

            try
            {
                accountManagerHost.Open();
                loginManagerHost.Open();
                Console.WriteLine("Server is running");
                Console.ReadLine();

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al iniciar el host: " + ex.ToString());
                Console.ReadLine();

                accountManagerHost.Abort();
                loginManagerHost.Abort();
            }
        }
    }
}