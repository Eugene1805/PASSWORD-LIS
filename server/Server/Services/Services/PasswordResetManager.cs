using Data.DAL.Interfaces;
using Services.Contracts;
using Services.Contracts.DTOs;
using System;
using System.ServiceModel;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class PasswordResetManager : IPasswordResetManager
    {
        private readonly IAccountRepository repository;
        public PasswordResetManager(IAccountRepository accountRepository)
        {
            repository = accountRepository;

        }
        public bool RequestPasswordResetCode(EmailVerificationDTO emailVerificationDTO)
        {
            throw new NotImplementedException();
        }

        public bool ResetPassword(PasswordResetDTO passwordResetDTO)
        {
            return repository.ResetPassword(passwordResetDTO.Email, BCrypt.Net.BCrypt.HashPassword(passwordResetDTO.NewPassword));
        }
    }
}
