using System;

namespace Services.Exceptions
{
    public class CouldNotCreateRoomException : Exception
    {
        public CouldNotCreateRoomException(string message) : base(message) 
        { 
        }
    }
}
