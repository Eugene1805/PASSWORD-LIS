using Data.DAL.Implementations;
using Data.DAL.Interfaces;
using Services.Contracts;
using Services.Contracts.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Services.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class LoginManager : ILoginManager
    {
        public readonly IAccountRepository repository;

        public LoginManager(IAccountRepository accountRepository)
        {
            repository = accountRepository;
        }


        public UserDTO Login(string email, string password)
        {
            var userAccount = repository.GetUserByEmail(email);

            if (userAccount != null)
            {
                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, userAccount.PasswordHash);

                if (isPasswordValid && userAccount.Player.Any())
                {
                    var player = userAccount.Player.FirstOrDefault();
                    if (player != null)
                    {
                        var userDTO = new UserDTO
                        {
                            UserAccountId = userAccount.Id,
                            PlayerId = player.Id,
                            Nickname = userAccount.Nickname,
                            Email = userAccount.Email,
                            FirstName = userAccount.FirstName,
                            LastName = userAccount.LastName,
                            PhotoId = userAccount.PhotoId ?? 0,

                            SocialAccounts = userAccount.SocialAccount.ToDictionary(sa => sa.Provider, sa => sa.Username)

                        };
                        return userDTO;
                    }
                }    
            }
            return null;
        }
    }
}
