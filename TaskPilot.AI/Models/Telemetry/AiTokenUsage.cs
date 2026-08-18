using System.Collections.Generic;

namespace TaskPilot.AI.Models.Telemetry;

public sealed record AiTokenUsage(int InputTokens, int CachedInputTokens, int OutputTokens)
{
    public int TotalTokens => InputTokens + OutputTokens;

    public static bool TryCreate(
        IReadOnlyDictionary<string, object?>? metadata,
        out AiTokenUsage usage)
    {
        usage = new AiTokenUsage(0, 0, 0);
        if (metadata == null)
            return false;

        object source = metadata;
        if (metadata.TryGetValue("Usage", out var usageObject) && usageObject != null)
            source = usageObject;

        var input = ReadInt(source, "InputTokenCount", "PromptTokens", "PromptTokenCount");
        var output = ReadInt(source, "OutputTokenCount", "CompletionTokens", "CandidatesTokenCount");
        var cached = ReadInt(source, "CachedContentTokenCount");

        var inputDetails = ReadObject(source, "InputTokenDetails");
        if (inputDetails != null)
            cached = ReadInt(inputDetails, "CachedTokenCount");

        if (!input.HasValue || !output.HasValue)
            return false;

        usage = new AiTokenUsage(
            Math.Max(0, input.Value),
            Math.Clamp(cached ?? 0, 0, Math.Max(0, input.Value)),
            Math.Max(0, output.Value));
        return true;
    }

    private static int? ReadInt(object source, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (source is IReadOnlyDictionary<string, object?> dictionary
                && dictionary.TryGetValue(propertyName, out var dictionaryValue)
                && dictionaryValue != null)
            {
                try
                {
                    return Convert.ToInt32(dictionaryValue);
                }
                catch (Exception)
                {
                    return null;
                }
            }

            var property = source.GetType().GetProperty(propertyName);
            if (property?.GetValue(source) is { } value)
            {
                try
                {
                    return Convert.ToInt32(value);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        return null;
    }

    private static object? ReadObject(object source, string propertyName) =>
        source.GetType().GetProperty(propertyName)?.GetValue(source);
}
