using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Util
{
    public enum CodeType { EmailVerification, PasswordReset }

    public interface IVerificationCodeService
    {
        string GenerateAndStoreCode(string identifier, CodeType type);
        bool ValidateCode(string identifier, string code, CodeType type);
        bool CanRequestCode(string identifier, CodeType type);
    }
    public class VerificationCodeService : IVerificationCodeService
    {
        private class CodeInfo
        {
            public string Code { get; set; }
            public DateTime CreationTime { get; set; }
            public DateTime ExpirationTime { get; set; }
            public CodeType Type { get; set; }
        }

        private readonly object lockObject = new object();
        private readonly Dictionary<string, CodeInfo> codes = new Dictionary<string, CodeInfo>();
        private readonly Random random = new Random();

        public string GenerateAndStoreCode(string identifier, CodeType type)
        {
            lock (lockObject)
            {
                var code = random.Next(100000, 999999).ToString();
                var now = DateTime.UtcNow;
                var codeInfo = new CodeInfo
                {
                    Code = code,
                    CreationTime = now,
                    ExpirationTime = now.AddMinutes(5),
                    Type = type
                };
                codes[identifier] = codeInfo;
                return code;
            }
        }

        public bool ValidateCode(string identifier, string code, CodeType type)
        {
            lock (lockObject)
            {
                if (!codes.TryGetValue(identifier, out CodeInfo codeInfo))
                    return false;

                // El código se elimina después del primer intento de validación
                codes.Remove(identifier);

                // El código y el propósito deben coincidir, y no debe haber expirado
                return codeInfo.Code == code &&
                       codeInfo.Type == type &&
                       DateTime.UtcNow < codeInfo.ExpirationTime;
            }
        }

        public bool CanRequestCode(string identifier, CodeType type)
        {
            lock (lockObject)
            {
                if (codes.TryGetValue(identifier, out CodeInfo existingCode) && existingCode.Type == type)
                {
                    // Si se pidió un código hace menos de 60 segundos, no se puede pedir otro.
                    return DateTime.UtcNow.Subtract(existingCode.CreationTime).TotalSeconds >= 60;
                }
                return true; // No hay código previo, así que se puede pedir uno.
            }
        }
    }
}
