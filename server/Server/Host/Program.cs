using Data.DAL.Implementations;
using log4net;
using log4net.Config;
using Services.Services;
using Services.Util;
using Services.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;

namespace Host
{
    static class Program
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));
        
        static void Main(string[] args)
        {
            XmlConfigurator.Configure();
            var hosts = new List<ServiceHost>();

            try
            {
                log.Info("The server started.");
                InitializeAndStartServices(hosts);
                
                Console.WriteLine("\n All services are running");
                Console.WriteLine("Press <Enter> to stop them.");
                Console.ReadLine();
            }
            catch (Exception ex) when (IsKnownStartupException(ex))
            {
                log.Error($"An error occurred during service startup: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString(), ex);
                Console.ReadLine();
            }
            finally
            {
                CleanupHosts(hosts);
            }
        }

        private static bool IsKnownStartupException(Exception ex)
        {
            return ex is ObjectDisposedException || 
                   ex is InvalidOperationException || 
                   ex is CommunicationObjectFaultedException || 
                   ex is CommunicationException || 
                   ex is TimeoutException;
        }

        private static void InitializeAndStartServices(List<ServiceHost> hosts)
        {
            var dbContextFactory = new DbContextFactory();
            var emailSender = new EmailSender();
            var codeService = new VerificationCodeService();
            var notificationService = new NotificationService(emailSender);
            var operationContextWrapper = new OperationContextWrapper();

            var accountRepository = new AccountRepository(dbContextFactory);
            var statisticsRepository = new StatisticsRepository(dbContextFactory);
            var playerRepository = new PlayerRepository(dbContextFactory);
            var friendshipRepository = new FriendshipRepository(dbContextFactory);
            var reportRepository = new ReportRepository(dbContextFactory);
            var banRepository = new BanRepository(dbContextFactory);
            var wordRepository = new WordRepository(dbContextFactory);
            var matchRepository = new MatchRepository(dbContextFactory);

            var accountManagerInstance = new AccountManager(accountRepository, notificationService, codeService);
            var loginManagerInstance = new LoginManager(accountRepository, notificationService, codeService);
            var verificationManagerInstance = new VerificationCodeManager(accountRepository, notificationService, 
                codeService);
            var passwordResetManagerInstance = new PasswordResetManager(accountRepository, notificationService, 
                
                codeService);
            var profileManagerInstance = new ProfileManager(accountRepository);
            var topPlayersManagerInstance = new TopPlayersManager(statisticsRepository);
            var gameManagerInstance = new GameManager(operationContextWrapper, wordRepository, matchRepository,
                playerRepository);
            var waitingRoomManagerInstance = new WaitingRoomManager(playerRepository, operationContextWrapper,
                gameManagerInstance, accountRepository, notificationService);
            var friendsManagerInstance = new FriendsManager(friendshipRepository, accountRepository,
                operationContextWrapper);
            var reportManagerInstance = new ReportManager(reportRepository, playerRepository, banRepository, 
                operationContextWrapper);

            hosts.Add(new ServiceHost(accountManagerInstance));
            hosts.Add(new ServiceHost(loginManagerInstance));
            hosts.Add(new ServiceHost(verificationManagerInstance));
            hosts.Add(new ServiceHost(passwordResetManagerInstance));
            hosts.Add(new ServiceHost(profileManagerInstance));
            hosts.Add(new ServiceHost(topPlayersManagerInstance));
            hosts.Add(new ServiceHost(friendsManagerInstance));
            hosts.Add(new ServiceHost(reportManagerInstance));
            hosts.Add(new ServiceHost(gameManagerInstance));
            hosts.Add(new ServiceHost(waitingRoomManagerInstance));

            foreach (var host in hosts)
            {
                OpenServiceHost(host);
            }
        }

        private static void OpenServiceHost(ServiceHost host)
        {
            var serviceName = host.Description?.ServiceType?.Name ?? host.GetType().Name;

            try
            {
                host.Open();
                
                foreach (var endpoint in host.Description.Endpoints)
                {
                    Console.WriteLine($"-> Service listening in: {endpoint.Address}");
                }
            }
            catch (AddressAccessDeniedException ex)
            {
                log.Error($"Failed to open service '{serviceName}'." +
                    $" The process lacks permissions to listen on the specified address/port." +
                    $" Try running as Administrator or reserving the URL with netsh. Details: {ex.Message}", ex);
                SafeAbort(host);
            }
            catch (AddressAlreadyInUseException ex)
            {
                log.Error($"Failed to open service '{serviceName}'. The address/port is already in use. " +
                    $"Ensure no other service is using the same base address. Details: {ex.Message}", ex);
                SafeAbort(host);
            }
            catch (Exception ex) when (IsKnownStartupException(ex))
            {
                log.Error($"Failed to open service '{serviceName}'. Details: {ex.Message}", ex);
                SafeAbort(host);
            }
            catch (Exception ex)
            {
                log.Error($"Failed to open service '{serviceName}' due to an unexpected error. Details: {ex.Message}", ex);
                SafeAbort(host);
            }
        }

        private static void CleanupHosts(List<ServiceHost> hosts)
        {
            foreach (var host in hosts.Where(h => h != null))
            {
                if (host.State == CommunicationState.Faulted)
                {
                    host.Abort();
                }
                else
                {
                    try
                    {
                        host.Close();
                    }
                    catch
                    {
                        host.Abort();
                    }
                }
            }
        }

        private static void SafeAbort(ServiceHost host)
        {
            try
            {
                host?.Abort();
            }
            catch
            {
                // Swallow any abort exceptions to avoid crashing the process during cleanup.
            }
        }
    }
}