using Data.Model;
using Data.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Data.DAL
{
    public static class AccountRepository
    {
        public static bool CreateAccount(UserAccount account)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                if (AccountAlreadyExist(account.Email))
                {
                    return false;
                }
                try
                {
                    context.UserAccount.Add(account);
                    context.Player.Add(new Player { UserAccount = account });
                    context.SaveChanges();
                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
           
        }
    }
}
