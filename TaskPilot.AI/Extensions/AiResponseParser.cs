using System;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace TaskPilot.AI.Extensions
{
    public static class AiResponseParser
    {
        public static T? Parse<T>(string? rawText, JsonSerializerOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return default;
            }

            var text = rawText.Trim();

            // Locate JSON boundaries
            int firstBrace = text.IndexOf('{');
            int lastBrace = text.LastIndexOf('}');
            
            int firstBracket = text.IndexOf('[');
            int lastBracket = text.LastIndexOf(']');
            
            int startIndex = -1;
            int endIndex = -1;

            // Determine if the outermost structure is an object or array
            if (firstBrace != -1 && (firstBracket == -1 || firstBrace < firstBracket))
            {
                startIndex = firstBrace;
                endIndex = lastBrace;
            }
            else if (firstBracket != -1)
            {
                startIndex = firstBracket;
                endIndex = lastBracket;
            }

            if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
            {
                text = text.Substring(startIndex, (endIndex - startIndex) + 1);
            }

            options ??= new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<T>(text, options);
        }
    }
}
