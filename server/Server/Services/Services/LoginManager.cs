using Data.DAL;
using Services.Contracts;
using Services.Contracts.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Services
{
    public class LoginManager : ILoginManager
    {
        public UserDTO Login(string email, string password)
        {
            var repository = new AccountRepository();
            var userAccount = repository.GetUserByEmail(email);

            if (userAccount != null)
            {
                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, userAccount.PasswordHash);

                if (isPasswordValid && userAccount.Player != null)
                {
                    var userDTO = new UserDTO
                    {
                        PlayerId = userAccount.Player.Id,
                        Nickname = userAccount.Nickname,
                        Email = userAccount.Email

                    };

                    return userDTO;
                }    
            }
            return null;
        }
    }
}
