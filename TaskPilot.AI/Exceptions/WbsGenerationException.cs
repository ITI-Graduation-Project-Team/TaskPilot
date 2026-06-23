namespace TaskPilot.AI.Exceptions
{
    public class WbsGenerationException : Exception
    {
        public string RawResponse { get; }

        public WbsGenerationException(
            string message,
            string rawResponse)
            : base(message)
        {
            RawResponse = rawResponse;
        }
    }
}
