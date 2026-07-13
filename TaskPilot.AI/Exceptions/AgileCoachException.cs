using System;

namespace TaskPilot.AI.Exceptions
{
    public class AgileCoachException : Exception
    {
        public AgileCoachException(string message) : base(message) { }
        public AgileCoachException(string message, Exception innerException) : base(message, innerException) { }
    }
}
