using System;

namespace Data.Exceptions
{
    public class DuplicateAccountException : Exception
    {
        public DuplicateAccountException(string message) : base(message) 
        {
        }
    }
}