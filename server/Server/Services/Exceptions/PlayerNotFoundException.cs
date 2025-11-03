using System;

namespace Services.Exceptions
{
    public class PlayerNotFoundException : Exception
    {
        public PlayerNotFoundException(string message) : base(message) 
        {
        }
    }
}
