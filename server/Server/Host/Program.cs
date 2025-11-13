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
                // We create the dependencies first so they can be used in every service
                var dbContextFactory = new DbContextFactory();

                var accountRepository = new AccountRepository();
                var emailSender = new EmailSender();
                var codeService = new VerificationCodeService();
                var notificationService = new NotificationService(emailSender);
                var statisticsRepository = new StatisticsRepository(dbContextFactory);
                var playerRepository = new PlayerRepository(dbContextFactory);
                var operationContextWrapper = new OperationContextWrapper();
                var friendshipRepository = new FriendshipRepository();
                var reportRepository = new ReportRepository(dbContextFactory);
                var banRepository = new BanRepository();
                var wordRepository = new WordRepository(dbContextFactory);
                var matchRepository = new MatchRepository(dbContextFactory);


                var accountManagerInstance = new AccountManager(accountRepository, notificationService, codeService);
                var loginManagerInstance = new LoginManager(accountRepository);
                var verificationManagerInstance = new VerificationCodeManager(accountRepository, notificationService, codeService);
                var passwordResetManagerInstance = new PasswordResetManager(accountRepository, notificationService, codeService);
                var profileManagerInstance = new ProfileManager(accountRepository);
                var topPlayersManagerInstance = new TopPlayersManager(statisticsRepository);
                var gameManagerInstance = new GameManager(operationContextWrapper, wordRepository, matchRepository, playerRepository);
                var waitingRoomManagerInstance = new WaitingRoomManager(playerRepository,operationContextWrapper,gameManagerInstance, accountRepository, notificationService);
                var friendsManagerInstance = new FriendsManager(friendshipRepository, accountRepository, operationContextWrapper);
                var reportManagerInstance = new ReportManager(reportRepository, playerRepository,banRepository,operationContextWrapper);

                
                var accountManagerHost = new ServiceHost(accountManagerInstance);
                var loginManagerHost = new ServiceHost(loginManagerInstance);
                var verificationManagerHost = new ServiceHost(verificationManagerInstance);
                var passwordResetManagerHost = new ServiceHost(passwordResetManagerInstance);
                var profileManagerHost = new ServiceHost(profileManagerInstance);
                var topPlayersManagerHost = new ServiceHost(topPlayersManagerInstance);
                var friendsManagerHost = new ServiceHost(friendsManagerInstance);
                var reportManagerHost = new ServiceHost(reportManagerInstance);
                var gameManagerHost = new ServiceHost(gameManagerInstance);
                var waitingRoomManagerHost = new ServiceHost(waitingRoomManagerInstance);
                

                hosts.Add(accountManagerHost);
                hosts.Add(loginManagerHost);
                hosts.Add(verificationManagerHost);
                hosts.Add(passwordResetManagerHost);
                hosts.Add(profileManagerHost);
                hosts.Add(topPlayersManagerHost);
                hosts.Add(friendsManagerHost);
                hosts.Add(reportManagerHost);
                hosts.Add(gameManagerHost);
                hosts.Add(waitingRoomManagerHost);

                
                foreach (var host in hosts)
                {
                    var serviceName = host.Description != null && host.Description.ServiceType != null
                        ? host.Description.ServiceType.Name
                        : host.GetType().Name;

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
                        log.Error($"Failed to open service '{serviceName}'. The process lacks permissions to listen on the specified address/port. Try running as Administrator or reserving the URL with netsh. Details: {ex.Message}", ex);
                        SafeAbort(host);
                    }
                    catch (AddressAlreadyInUseException ex)
                    {
                        log.Error($"Failed to open service '{serviceName}'. The address/port is already in use. Ensure no other service is using the same base address. Details: {ex.Message}", ex);
                        SafeAbort(host);
                    }
                    catch (ObjectDisposedException ex)
                    {
                        log.Error($"Failed to open service '{serviceName}'. The host or one of its dependencies was disposed. Details: {ex.Message}", ex);
                        SafeAbort(host);
                    }
                    catch (InvalidOperationException ex)
                    {
                        log.Error($"Failed to open service '{serviceName}'. Invalid configuration or state. Verify endpoints, bindings, and behaviors. Details: {ex.Message}", ex);
                        SafeAbort(host);
                    }
                    catch (CommunicationObjectFaultedException ex)
                    {
                        log.Error($"Failed to open service '{serviceName}'. The communication object is faulted. Details: {ex.Message}", ex);
                        SafeAbort(host);
                    }
                    catch (CommunicationException ex)
                    {
                        log.Error($"Failed to open service '{serviceName}'. A communication error occurred while opening the listener. Details: {ex.Message}", ex);
                        SafeAbort(host);
                    }
                    catch (TimeoutException ex)
                    {
                        log.Error($"Failed to open service '{serviceName}' due to timeout. Consider increasing open timeouts. Details: {ex.Message}", ex);
                        SafeAbort(host);
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Failed to open service '{serviceName}' due to an unexpected error. Details: {ex.Message}", ex);
                        SafeAbort(host);
                    }
                }

                Console.WriteLine("\n All services are running");
                Console.WriteLine("Press <Enter> to stop them.");
                Console.ReadLine();
            }
            catch (ObjectDisposedException ex)
            {
                log.Error("A disposal-related error occurred during service startup.", ex);
            }
            catch (InvalidOperationException ex)
            {
                log.Error("An invalid operation occurred during service startup.", ex);
            }
            catch(CommunicationObjectFaultedException ex) { 
                log.Error("A communication object fault occurred during service startup.", ex);
            } 
            catch(CommunicationException ex)
            {
                log.Error("A communication error occurred during service startup.", ex);
            }
            catch(TimeoutException ex)
            {
                log.Error("A timeout occurred during service startup.", ex);
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString(), ex);
                Console.ReadLine();
            }
            finally
            {
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