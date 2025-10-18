using System;
using System.Collections.Generic;

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
        sealed class CodeInfo
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

                return code;
            }
        }
           
        public bool ValidateCode(string identifier, string code, CodeType type, bool consume = true)
        {
            var key = GetKey(identifier, type);
            lock (lockObject)
            {
                if (!codes.TryGetValue(key, out CodeInfo codeInfo))
                {
                    return false;
                }
                if (DateTime.UtcNow >= codeInfo.ExpirationTime)
                {
                    codes.Remove(key);
                    return false;
                }
                if (codeInfo.Type != type)
                {
                    return false;
                }
                if (codeInfo.Code != code) {
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
