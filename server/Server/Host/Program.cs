using Services.Contracts;
using Services.Services;
using System;
using System.ServiceModel;

namespace Host
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            ServiceHost host = null;
            try
            {
                var baseAddress = new Uri("net.tcp://localhost:8090/PasswordLis");
                host = new ServiceHost(typeof(AccountManager), baseAddress);
                var binding = new NetTcpBinding();
                host.AddServiceEndpoint(typeof(IAccountManager), binding, "AccountManager");

                host.Open();

                Console.WriteLine(" El servicio está en ejecución.");
                Console.ReadLine();
                host.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Ocurrió un error: {ex.ToString()}");
                Console.ReadLine();
                host?.Abort();
            }
        }
    }
}