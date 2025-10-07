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
    public class AccountRepository
    {
        public bool CreateAccount(UserAccount acount)
        {
            using (var context = new PasswordLISEntities(Connection.GetConnectionString()))
            {
                try
                {
                    context.UserAccount.Add(acount);
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
