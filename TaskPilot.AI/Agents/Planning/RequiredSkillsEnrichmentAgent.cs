using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Models.Planning;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Enums;

namespace TaskPilot.AI.Agents.Planning;

public class RequiredSkillsEnrichmentAgent
{
    private readonly IAiKernelService _kernelService;
    private readonly IPromptLoaderService _promptLoader;
    private readonly ILogger<RequiredSkillsEnrichmentAgent> _logger;
    private readonly ITelemetryAccumulator _telemetry;
    private readonly object _functionLock = new();
    private Task<KernelFunction>? _functionTask;

    public RequiredSkillsEnrichmentAgent(
        IAiKernelService kernelService,
        IPromptLoaderService promptLoader,
        ILogger<RequiredSkillsEnrichmentAgent> logger,
        ITelemetryAccumulator telemetry)
    {
        _kernelService = kernelService;
        _promptLoader = promptLoader;
        _logger = logger;
        _telemetry = telemetry;
    }

    public virtual async Task<Result<List<GeneratedTaskRequiredSkills>>> EnrichBatchAsync(
        IReadOnlyCollection<SkillEnrichmentTaskInput> tasks,
        IReadOnlyCollection<string> availableSkills,
        CancellationToken cancellationToken = default)
    {
        if (tasks.Count == 0)
            return Result.Success(new List<GeneratedTaskRequiredSkills>());

        try
        {
            var function = await GetFunctionAsync();
            // Parallel batches never share mutable Kernel state.
            var kernel = _kernelService.CreateKernel(ModelConstants.CheapModel);
            var arguments = new KernelArguments
            {
                ["tasks"] = JsonSerializer.Serialize(tasks, JsonOptions),
                ["availableSkills"] = JsonSerializer.Serialize(availableSkills, JsonOptions)
            };

            var stopwatch = Stopwatch.StartNew();
            var response = await kernel.InvokeAsync(function, arguments, cancellationToken);
            stopwatch.Stop();

            _telemetry.RecordCall(
                response.Metadata,
                stopwatch.ElapsedMilliseconds,
                nameof(RequiredSkillsEnrichmentAgent),
                ModelConstants.CheapModel,
                _logger);

            var rawJson = response.GetValue<string>() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rawJson))
                return Failure("The AI returned an empty response.");

            var parsed = TaskPilot.AI.Extensions.AiResponseParser.Parse<List<GeneratedTaskRequiredSkills>>(
                TryRepairJsonArray(rawJson),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed == null)
                return Failure("The AI response was not a valid batched required-skills array.");

            var validResults = parsed
                .Where(item => item.TaskId != Guid.Empty)
                .GroupBy(item => item.TaskId)
                .Select(group => new GeneratedTaskRequiredSkills
                {
                    TaskId = group.Key,
                    Skills = ValidateSkills(group.SelectMany(item => item.Skills ?? new List<GeneratedRequiredSkill>()).ToList())
                })
                .Where(item => item.Skills.Count > 0)
                .ToList();

            return Result.Success(validResults);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Required-skills batch response contained malformed JSON.");
            return Failure("The AI response contained malformed JSON.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Required-skills batch request failed.");
            return Failure($"The AI request failed: {ex.GetType().Name}.");
        }
    }

    internal static List<GeneratedRequiredSkill> ValidateSkills(List<GeneratedRequiredSkill>? skills)
    {
        if (skills == null || skills.Count == 0)
            return new List<GeneratedRequiredSkill>();

        return skills
            .Where(skill => !string.IsNullOrWhiteSpace(skill.SkillName)
                && Enum.TryParse<SkillLevel>(skill.RequiredLevel, true, out _))
            .Select(skill => new GeneratedRequiredSkill
            {
                SkillName = skill.SkillName.Trim(),
                RequiredLevel = skill.RequiredLevel.Trim()
            })
            .GroupBy(skill => skill.SkillName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private Task<KernelFunction> GetFunctionAsync()
    {
        lock (_functionLock)
        {
            return _functionTask ??= LoadFunctionAsync();
        }
    }

    private async Task<KernelFunction> LoadFunctionAsync()
    {
        var promptYaml = await _promptLoader.LoadAsync("Planning/RequiredSkillsEnrichment.yaml");
        return KernelFunctionYaml.FromPromptYaml(promptYaml);
    }

    private static Result<List<GeneratedTaskRequiredSkills>> Failure(string reason) =>
        Result.Failure<List<GeneratedTaskRequiredSkills>>(new Error(
            "REQUIRED_SKILLS_NO_VALID_RESULT",
            ErrorType.Failure,
            reason));

    private static string TryRepairJsonArray(string raw)
    {
        raw = raw.Trim();
        if (raw.StartsWith("```", StringComparison.Ordinal))
        {
            var lines = raw.Split('\n').ToList();
            if (lines.Count > 0 && lines[0].StartsWith("```", StringComparison.Ordinal)) lines.RemoveAt(0);
            if (lines.Count > 0 && lines[^1].TrimStart().StartsWith("```", StringComparison.Ordinal)) lines.RemoveAt(lines.Count - 1);
            raw = string.Join('\n', lines).Trim();
        }

        if (!raw.StartsWith("[", StringComparison.Ordinal) || raw.EndsWith("]", StringComparison.Ordinal))
            return raw;

        var lastClosingBrace = FindLastCompleteRootObject(raw);
        return lastClosingBrace < 0 ? "[]" : raw[..(lastClosingBrace + 1)] + "]";
    }

    private static int FindLastCompleteRootObject(string raw)
    {
        var depth = 0;
        var inString = false;
        var escape = false;
        var lastClosingBrace = -1;

        for (var index = 0; index < raw.Length; index++)
        {
            var character = raw[index];
            if (escape) { escape = false; continue; }
            if (character == '\\' && inString) { escape = true; continue; }
            if (character == '"') { inString = !inString; continue; }
            if (inString) continue;

            if (character == '{') depth++;
            else if (character == '}' && --depth == 0) lastClosingBrace = index;
        }

        return lastClosingBrace;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
