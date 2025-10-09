using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Util
{
    public interface INotificationService
    {
        Task SendAccountVerificationEmailAsync(string email, string code);
        Task SendPasswordResetEmailAsync(string email, string code);
    }
    public class NotificationService : INotificationService
    {
        private readonly IEmailSender sender;

        public NotificationService(IEmailSender emailSender)
        {
            sender = emailSender;
        }

        public Task SendAccountVerificationEmailAsync(string email, string code)
        {
            var subject = "Código de Verificación de Cuenta";
            var body = $"<html><body><h2>¡Gracias por registrarte!</h2><p>Tu código de verificación es:</p><h1>{code}</h1></body></html>";
            return sender.SendEmailAsync(email, subject, body);
        }

        public Task SendPasswordResetEmailAsync(string email, string code)
        {
            var subject = "Restablecimiento de Contraseña";
            var body = $"<html><body><h2>Solicitud de cambio de contraseña</h2><p>Tu código para restablecer la contraseña es:</p><h1>{code}</h1></body></html>";
            return sender.SendEmailAsync(email, subject, body);
        }
    }
}
