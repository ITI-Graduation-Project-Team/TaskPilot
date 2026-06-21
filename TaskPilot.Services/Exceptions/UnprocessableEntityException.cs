using System;

namespace TaskPilot.Services.Exceptions
{
    public class UnprocessableEntityException : Exception
    {
        public UnprocessableEntityException(string message) : base(message)
        {
        }
    }
}
