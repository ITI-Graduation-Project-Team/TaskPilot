using System;

namespace TaskPilot.AI.Exceptions
{
    public class TechStackAdvisorException : Exception
    {
        public string RawResponse { get; }

        public TechStackAdvisorException(string message, string rawResponse)
            : base(message)
        {
            RawResponse = rawResponse;
        }
    }
}
