using System;
using System.Collections.Generic;
using System.ServiceModel;
using Services.Services;
using Data.DAL;
using Services.Util;
using Data.DAL.Implementations;

namespace Host
{
    class Program
    {
        static void Main(string[] args)
        {
            // Lista para manejar todos los hosts de manera ordenada
            var hosts = new List<ServiceHost>();

            try
            {
                // --- PASO 1: Crear las dependencias UNA SOLA VEZ ---
                // Estas instancias se compartirán entre todos los servicios que las necesiten.
                var accountRepository = new AccountRepository();
                var emailSender = new EmailSender();

                // --- PASO 2: Crear las INSTANCIAS de cada servicio ---
                // Se inyectan las dependencias compartidas en cada constructor.
                var accountManagerInstance = new AccountManager(accountRepository, emailSender);
                //var loginManagerInstance = new LoginManager(accountRepository); // Asumiendo que LoginManager necesita el repositorio
                var verificationManagerInstance = new VerificationCodeManager(accountRepository,emailSender); // Asumiendo que VerificationCodeManager necesita el repositorio

                // --- PASO 3: Crear un ServiceHost para CADA instancia de servicio ---
                var accountManagerHost = new ServiceHost(accountManagerInstance);
                //var loginManagerHost = new ServiceHost(loginManagerInstance);
                var verificationManagerHost = new ServiceHost(verificationManagerInstance);

                // Agregarlos a la lista para manejarlos fácilmente
                hosts.Add(accountManagerHost);
               // hosts.Add(loginManagerHost);
                hosts.Add(verificationManagerHost);

                // --- PASO 4: Abrir todos los hosts ---
                foreach (var host in hosts)
                {
                    host.Open();
                    // Imprime la dirección de cada endpoint para saber que está escuchando
                    foreach (var endpoint in host.Description.Endpoints)
                    {
                        Console.WriteLine($"-> Servicio escuchando en: {endpoint.Address}");
                    }
                }

                Console.WriteLine("\n Todos los servicios están en ejecución.");
                Console.WriteLine("Presiona <Enter> para detenerlos.");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Ocurrió un error al iniciar los servicios: {ex.ToString()}");
                Console.ReadLine();
            }
            finally
            {
                // --- PASO 5: Cerrar todos los hosts de forma segura ---
                foreach (var host in hosts)
                {
                    if (host != null)
                    {
                        if (host.State == CommunicationState.Faulted)
                        {
                            host.Abort();
                        }
                        else
                        {
                            host.Close();
                        }
                    }
                }
            }
        }
    }
}