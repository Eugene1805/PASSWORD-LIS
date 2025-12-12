using System.Threading.Tasks;

namespace Services.Util
{
    public interface INotificationService
    {
        Task SendAccountVerificationEmailAsync(string email, string code);
        Task SendPasswordResetEmailAsync(string email, string code);
        Task SendGameInvitationEmailAsync(string email, string gameCode, string inviterNickname);
    }
    public class NotificationService : INotificationService
    {
        private readonly IEmailSender sender;

        public NotificationService(IEmailSender EmailSender)
        {
            sender = EmailSender;
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

        public Task SendGameInvitationEmailAsync(string email, string gameCode, string inviterNickname)
        {
            var subject = "¡Has sido invitado a una partida de PASSWORD LIS!";
            var body = $"<html><body><h2>¡Hola!</h2><p>Tu amigo {inviterNickname} te ha invitado a unirte a su partida. Ingresa el siguiente código en el juego para unirte:</p><h1>{gameCode}</h1></body></html>";

            return sender.SendEmailAsync(email, subject, body);
        }
    }
}
