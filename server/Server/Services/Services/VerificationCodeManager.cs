using Data.DAL.Implementations;
using Data.DAL.Interfaces;
using Services.Contracts;
using Services.Contracts.DTOs;
using Services.Util;
using System;
using System.Collections.Generic;
using System.ServiceModel;

namespace Services.Services
{
    internal class VerificationCodeInfo
    {
        public string Code { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime ExpirationTime { get; set; }
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class VerificationCodeManager : IAccountVerificationManager
    {
        private static readonly Dictionary<string, VerificationCodeInfo> codes = new Dictionary<string, VerificationCodeInfo>();
        private static readonly Random random = new Random();
        private readonly IAccountRepository repository;
        private readonly IEmailSender sender;    

        public VerificationCodeManager(IAccountRepository accountRepository, IEmailSender emailSender)
        {
            repository = accountRepository;
            sender = emailSender;
        }
        
        public static string GenerateCode(string email)
        {
            var code = random.Next(100000, 999999).ToString();
            var now = DateTime.UtcNow;

            var codeInfo = new VerificationCodeInfo
            {
                Code = code,
                CreationTime = now,
                ExpirationTime = now.AddMinutes(5)
            };

            codes[email] = codeInfo;

            return code;
        }

        public static bool VerifyCode(string email, string code)
        {
            // Si no existe un código para ese email, falla
            if (!codes.TryGetValue(email, out VerificationCodeInfo codeInfo))
            {
                return false;
            }

            // Importante: Una vez que se intenta verificar, se elimina el código para evitar reintentos.
            codes.Remove(email);

            // Comprueba si el código es correcto Y no ha expirado
            if (codeInfo.Code == code && DateTime.UtcNow < codeInfo.ExpirationTime)
            {
                return true; // Éxito
            }

            return false; // El código es incorrecto o ha expirado
        }

        public bool VerifyEmail(EmailVerificationDTO emailVerificationDTO)
        {
            if (VerifyCode(emailVerificationDTO.Email, emailVerificationDTO.VerificationCode))
            {
                return repository.VerifyEmail(emailVerificationDTO.Email);
            }

            return false;
        }

        public bool ResendVerificationCode(string email)
        {
            // 1. Verificar si existe un código previo para controlar el tiempo
            if (codes.TryGetValue(email, out VerificationCodeInfo existingCodeInfo) || DateTime.UtcNow.Subtract(existingCodeInfo.CreationTime).TotalSeconds < 60)
            {
                return false; // Indicar al cliente que debe esperar
            }

            // 3. Si la comprobación pasa, generar y enviar un nuevo código
            var newCode = GenerateCode(email);
            _ = sender.SendVerificationEmailAsync(email, newCode);

            return true;
        }
    }
}
