using Data.DAL.Implementations;
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
        public UserDTO Login(string email, string password)
        {/*
            var userAccount = AccountRepository.GetUserByEmail(email);

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
                            PlayerId = player.Id,
                            Nickname = userAccount.Nickname,
                            Email = userAccount.Email

                        };
                        return userDTO;
                    }
                }    
            }*/
            return null;
        }
    }
}
