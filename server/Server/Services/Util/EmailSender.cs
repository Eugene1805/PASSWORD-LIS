using log4net;
using Services.Contracts.Enums;
using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Util
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string recipientEmail, string subject, string body);
    }

    public class EmailSender : IEmailSender
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(EmailSender));
        public async Task SendEmailAsync(string recipientEmail, string subject, string body)
        {
            try
            {
                var (smtpUser, smtpPass, smtpHost, smtpPort) = LoadSmtpConfiguration();

                var message = new MailMessage
                {
                    From = new MailAddress(smtpUser, "PASSWORD LIS"),
                    Subject = subject,
                    IsBodyHtml = true,
                    Body = body
                };
                message.To.Add(recipientEmail);

                using (var smtpClient = new SmtpClient(smtpHost, smtpPort))
                {
                    smtpClient.Credentials = new NetworkCredential(smtpUser, smtpPass);
                    smtpClient.EnableSsl = true;

                    await smtpClient.SendMailAsync(message);
                }
            }
            catch (ConfigurationErrorsException ex)
            {
                log.Error("SMTP configuration error", ex);
                throw FaultExceptionFactory.Create(
                    ServiceErrorCode.EmailConfigurationError,
                    "EMAIL_CONFIGURATION_ERROR",
                    "Email service configuration error"
                );
            }
            catch (FormatException ex)
            {
                log.Error("Invalid SMTP configuration format", ex);
                throw FaultExceptionFactory.Create(
                    ServiceErrorCode.EmailConfigurationError,
                    "EMAIL_CONFIGURATION_ERROR",
                    "Invalid email service configuration"
                );
            }
            catch (SmtpException ex)
            {
                log.Error($"SMTP error sending to {recipientEmail}", ex);
                throw FaultExceptionFactory.Create(
                    ServiceErrorCode.EmailSendingError,
                    "EMAIL_SENDING_ERROR",
                    $"Failed to send email: {ex.StatusCode}"
                );
            }
            catch (Exception ex)
            {
                log.Error($"Unexpected error sending email to {recipientEmail}", ex);
                throw FaultExceptionFactory.Create(
                    ServiceErrorCode.EmailSendingError,
                    "EMAIL_SENDING_ERROR",
                    "Failed to send email"
                );
            }
        }

        private static (string user, string pass, string host, int port) LoadSmtpConfiguration()
        {
            var smtpUser = Environment.GetEnvironmentVariable("SMTP_USER") ??
                          ConfigurationManager.AppSettings["SmtpUser"];
            var smtpPass = Environment.GetEnvironmentVariable("SMTP_PASS") ??
                          ConfigurationManager.AppSettings["SmtpPass"];
            var smtpHost = ConfigurationManager.AppSettings["SmtpHost"];
            var smtpPort = int.Parse(ConfigurationManager.AppSettings["SmtpPort"]);

            if (string.IsNullOrWhiteSpace(smtpUser))
            {
                throw FaultExceptionFactory.Create(
                    ServiceErrorCode.EmailConfigurationError,
                    "EMAIL_CONFIGURATION_ERROR",
                    "SMTP user configuration is missing"
                );
            }
            return (smtpUser , smtpPass, smtpHost, smtpPort);
        }
    }
}
