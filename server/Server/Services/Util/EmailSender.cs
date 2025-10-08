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
        Task SendVerificationEmailAsync(string recipientEmail, string code);
    }
    public class EmailSender : IEmailSender
    {
        public async Task SendVerificationEmailAsync(string recipientEmail, string code)
        {
            var smtpUser = Environment.GetEnvironmentVariable("SMTP_USER") ?? ConfigurationManager.AppSettings["SmtpUser"];
            var smtpPass = Environment.GetEnvironmentVariable("SMTP_PASS") ?? ConfigurationManager.AppSettings["SmtpPass"];
            var smtpHost = ConfigurationManager.AppSettings["SmtpHost"];
            var smtpPort = int.Parse(ConfigurationManager.AppSettings["SmtpPort"]);
            

            var message = new MailMessage
            {
                From = new MailAddress(smtpUser, "PASSWORD LIS"),
                Subject = "Código de Verificación de Cuenta",
                IsBodyHtml = true,
                Body = $"<html><body><h2>¡Gracias por registrarte!</h2><p>Tu código de verificación es:</p><h1>{code}</h1><p>Este código expira en 5 minutos.</p></body></html>"
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
