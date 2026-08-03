using System;

namespace TaskPilot.AI.Helpers
{
    public static class TokenHelper
    {
        // A conservative heuristic for token estimation (roughly 4 characters per token)
        // Can be replaced with Microsoft.ML.Tokenizers if exact counting is required.
        public static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            return (int)Math.Ceiling(text.Length / 4.0);
        }
    }
}
