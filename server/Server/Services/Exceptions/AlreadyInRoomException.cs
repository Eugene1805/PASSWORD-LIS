using System;

namespace Services.Exceptions
{
    public class AlreadyInRoomException : Exception
    {
        public AlreadyInRoomException(string message) : base(message) 
        {
        }
    }
}
