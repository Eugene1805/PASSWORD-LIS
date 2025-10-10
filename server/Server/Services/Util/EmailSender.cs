using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace Services.Util

{

    public interface IEmailSender
    {
        Task SendEmailAsync(string recipientEmail, string subject, string body);
    }

    public class EmailSender : IEmailSender
    {
        public async Task SendEmailAsync(string recipientEmail, string subject, string body)
        {
            var smtpUser = Environment.GetEnvironmentVariable("SMTP_USER") ?? ConfigurationManager.AppSettings["SmtpUser"];
            var smtpPass = Environment.GetEnvironmentVariable("SMTP_PASS") ?? ConfigurationManager.AppSettings["SmtpPass"];
            var smtpHost = ConfigurationManager.AppSettings["SmtpHost"];
            var smtpPort = int.Parse(ConfigurationManager.AppSettings["SmtpPort"]);

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
    }
}
