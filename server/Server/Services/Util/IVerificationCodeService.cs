using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Services.Util
{
    public enum CodeType 
    { 
        EmailVerification, 
        PasswordReset 
    }

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
        private const int CodeLength = 6;
        private const int CodeValidityMinutes = 5;
        public const int RequestColdownSeconds = 60;
        private const int InvalidCodeLengthLimit = 0;
        private static string GetKey(string identifier, CodeType type)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException("Identifier cannot be null or empty", nameof(identifier));
            }
            return $"{type}:{identifier}";
        }
        public string GenerateAndStoreCode(string identifier, CodeType type)
        {
            var key = GetKey(identifier, type);
            lock (lockObject)
            {
                var code = GenerateRandomCode(CodeLength);
                var now = DateTime.UtcNow;
                var codeInfo = new CodeInfo
                {
                    Code = code,
                    CreationTime = now,
                    ExpirationTime = now.AddMinutes(CodeValidityMinutes),
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
                if (codes.TryGetValue(GetKey(identifier, type),
                    out CodeInfo existingCode) && existingCode.Type == type)
                {
                    return DateTime.UtcNow.Subtract(existingCode.CreationTime).TotalSeconds >= RequestColdownSeconds;
                }
                return true; 
            }
        }

        private string GenerateRandomCode(int length)
        {
            const int MinimumCodeValue = 100000;
            const int MaximumCodeValue = 999999;
            if (length <= InvalidCodeLengthLimit)
            {
                return random.Next(MinimumCodeValue, MaximumCodeValue).ToString();
            }
            const string AllowedDigits = "0123456789";

            var chars = new char[length];
            var buffer = new byte[length];

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(buffer);
            }

            for (int i = 0; i < length; i++)
            {
                chars[i] = AllowedDigits[buffer[i] % AllowedDigits.Length];
            }

            return new string(chars);
        }
    }
}
