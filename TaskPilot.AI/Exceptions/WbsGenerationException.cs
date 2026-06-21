namespace TaskPilot.AI.Exceptions
{
    public class WbsGenerationException : Exception
    {
        public WbsGenerationException(string message) : base(message) { }
        public WbsGenerationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
