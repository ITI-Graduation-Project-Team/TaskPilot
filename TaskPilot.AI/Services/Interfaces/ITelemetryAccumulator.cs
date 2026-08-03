namespace TaskPilot.AI.Services.Interfaces
{
    public interface ITelemetryAccumulator
    {
        void AddTokens(int input, int output);
        void AddTime(long elapsedMs);
        void RecordCall(System.Collections.Generic.IReadOnlyDictionary<string, object?>? metadata, long elapsedMs, string agentName, string modelName, Microsoft.Extensions.Logging.ILogger logger);
        int TotalInputTokens { get; }
        int TotalOutputTokens { get; }
        long TotalElapsedMs { get; }
        void Reset();
    }
}
