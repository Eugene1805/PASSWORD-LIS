using System;

namespace Services.Exceptions
{
    public class FullRoomException : Exception
    {
        public FullRoomException(string message) : base(message) 
        {
        }
    }
}
