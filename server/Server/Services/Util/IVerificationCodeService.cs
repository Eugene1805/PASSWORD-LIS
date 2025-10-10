using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Services.Util
{
    public enum CodeType { EmailVerification, PasswordReset }

    public interface IVerificationCodeService
    {
        string GenerateAndStoreCode(string identifier, CodeType type);
        bool ValidateCode(string identifier, string code, CodeType type, bool consume = true);
        bool CanRequestCode(string identifier, CodeType type);
    }
    public class VerificationCodeService : IVerificationCodeService
    {
        private readonly Guid guid = Guid.NewGuid();
        public VerificationCodeService()
        {
            Console.WriteLine($"<<<<< NUEVA INSTANCIA de VerificationCodeService CREADA, ID: {guid} >>>>>");
        }
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
        private static string GetKey(string identifier, CodeType type)
        {
            return $"{type}:{identifier}";
        }
        public string GenerateAndStoreCode(string identifier, CodeType type)
        {
            Console.WriteLine($"[GenerateAndStoreCode] Operando en la instancia ID: {guid}");
            var key = GetKey(identifier, type);
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
                codes[key] = codeInfo;
                Console.WriteLine($"[GenerateAndStoreCode] Diccionario contiene {codes.Count} elementos:");

                foreach (var kvp in codes)
                    Console.WriteLine($"  {kvp.Key} => Code: {kvp.Value.Code}, Type: {kvp.Value.Type}, Exp: {kvp.Value.ExpirationTime:O}");

                return code;
            }
        }
           
        public bool ValidateCode(string identifier, string code, CodeType type, bool consume = true)
        {
            Console.WriteLine($"[ValidateCode] Operando en la instancia ID: {guid}");
            var key = GetKey(identifier, type);
            lock (lockObject)
            {
                if (!codes.TryGetValue(key, out CodeInfo codeInfo))
                {
                    
                    Console.WriteLine("No se pudo obtener el valor de ese correo con ese tipo");
                    Console.WriteLine("El mapa contiene los valores:");
                    Console.WriteLine($"[ValidateCode] ❌ Clave no encontrada: {key}");
                    Console.WriteLine($"[ValidateCode] Diccionario contiene {codes.Count} elementos:");

                    foreach (var kvp in codes)
                        Console.WriteLine($"  {kvp.Key} => Code: {kvp.Value.Code}, Type: {kvp.Value.Type}, Exp: {kvp.Value.ExpirationTime:O}");

                    return false;
                }
                if (DateTime.UtcNow >= codeInfo.ExpirationTime)
                {
                    Console.WriteLine("El codigo expiro");
                    codes.Remove(key);
                    return false;
                }
                if (codeInfo.Type != type)
                {
                    Console.WriteLine("El tipo no coincide");
                    return false;
                }
                if (codeInfo.Code != code) {
                    Console.WriteLine("El codigo no coincide");
                    return false;
                }
                if (consume)
                {
                    codes.Remove(key);
                }

                return true;
            }
        }
        
        public bool CanRequestCode(string identifier, CodeType type)
        {
            lock (lockObject)
            {
                if (codes.TryGetValue(GetKey(identifier, type), out CodeInfo existingCode) && existingCode.Type == type)
                {
                    return DateTime.UtcNow.Subtract(existingCode.CreationTime).TotalSeconds >= 60;
                }
                return true; 
            }
        }
    }
}
