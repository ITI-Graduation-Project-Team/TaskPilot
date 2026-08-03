using System;
using Microsoft.Extensions.Logging;

namespace TaskPilot.AI.Services
{
    public class TelemetryAccumulator : Interfaces.ITelemetryAccumulator
    {
        private int _totalInputTokens;
        private int _totalOutputTokens;
        private long _totalElapsedMs;
        private readonly object _lock = new object();

        public void AddTokens(int input, int output)
        {
            lock (_lock)
            {
                _totalInputTokens += input;
                _totalOutputTokens += output;
            }
        }

        public void AddTime(long elapsedMs)
        {
            lock (_lock)
            {
                _totalElapsedMs += elapsedMs;
            }
        }

        public void RecordCall(System.Collections.Generic.IReadOnlyDictionary<string, object?>? metadata, long elapsedMs, string agentName, string modelName, Microsoft.Extensions.Logging.ILogger logger)
        {
            int inputTokens = 0, outputTokens = 0;
            if (metadata != null && metadata.TryGetValue("Usage", out var usageObj) && usageObj != null)
            {
                var t = usageObj.GetType();
                var inProp = t.GetProperty("PromptTokens");
                var outProp = t.GetProperty("CompletionTokens");
                if (inProp != null) inputTokens = (int)(inProp.GetValue(usageObj) ?? 0);
                if (outProp != null) outputTokens = (int)(outProp.GetValue(usageObj) ?? 0);
            }

            AddTokens(inputTokens, outputTokens);
            AddTime(elapsedMs);

            logger.LogInformation("AI call completed: Agent={Agent} Model={Model} InputTokens={InputTokens} OutputTokens={OutputTokens} ElapsedMs={ElapsedMs}", agentName, modelName, inputTokens, outputTokens, elapsedMs);
        }

        public int TotalInputTokens => _totalInputTokens;
        public int TotalOutputTokens => _totalOutputTokens;
        public long TotalElapsedMs => _totalElapsedMs;

        public void Reset()
        {
            lock (_lock)
            {
                _totalInputTokens = 0;
                _totalOutputTokens = 0;
                _totalElapsedMs = 0;
            }
        }
    }
}
