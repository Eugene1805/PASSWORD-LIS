using System;
using System.ServiceModel;
using System.ServiceModel.Description; // Necesario para los metadatos
using Services.Contracts;
using Services.Services;

namespace Host
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            ServiceHost host = null;
            try
            {
                // --- PASO 1: Definir ambas direcciones base ---
                var tcpBaseAddress = new Uri("net.tcp://localhost:8090/PasswordLis");
                var httpBaseAddress = new Uri("http://localhost:8091/PasswordLis"); // NUEVO: Dirección para HTTP

                // Crear el host pasándole AMBAS direcciones base
                host = new ServiceHost(typeof(AccountManager), tcpBaseAddress, httpBaseAddress);

                // --- PASO 2: Agregar ambos endpoints ---

                // Endpoint de TCP (el que ya tenías)
                var tcpBinding = new NetTcpBinding();
                host.AddServiceEndpoint(typeof(IAccountManager), tcpBinding, "AccountManager");

                // Endpoint de HTTP (NUEVO)
                var httpBinding = new BasicHttpBinding();
                host.AddServiceEndpoint(typeof(IAccountManager), httpBinding, "AccountManager");

                // --- PASO 3: Habilitar metadatos por HTTP (NUEVO y MUY IMPORTANTE) ---
                var metadataBehavior = new ServiceMetadataBehavior
                {
                    HttpGetEnabled = true // Permite que los clientes accedan a los metadatos (WSDL)
                };
                host.Description.Behaviors.Add(metadataBehavior);

                // Abrir el host
                host.Open();

                Console.WriteLine("✅ El servicio está en ejecución.");
                Console.WriteLine("Escuchando en:");
                foreach (var endpoint in host.Description.Endpoints)
                {
                    Console.WriteLine($" -> {endpoint.Address}");
                }
                Console.WriteLine("Presiona <Enter> para detener el servicio.");
                Console.ReadLine();

                host.Close();
            }
            catch (Exception ex)
            {
                // Nota: Si este código falla, puede ser porque necesitas permisos de administrador
                // para registrar la dirección HTTP. Intenta ejecutar Visual Studio como Administrador.
                Console.WriteLine($"❌ Ocurrió un error: {ex.Message}");
                Console.ReadLine();
                host?.Abort();
            }
        }
    }
}