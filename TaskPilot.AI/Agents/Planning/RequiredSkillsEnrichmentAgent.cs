using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.AI.Models.Planning;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.AI.Constants;
using TaskPilot.Models.Enums;

namespace TaskPilot.AI.Agents.Planning
{
    public class RequiredSkillsEnrichmentAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;
        private readonly ILogger<RequiredSkillsEnrichmentAgent> _logger;
        private readonly ITelemetryAccumulator _telemetry;

        // Fix 3: cached once per scoped lifetime — avoids 50× disk reads + Kernel builds
        private KernelFunction? _cachedFunction;
        private Kernel? _cachedKernel;

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

        public virtual async Task<Result<List<GeneratedRequiredSkill>>> EnrichAsync(
            string titleEn,
            string descriptionEn,
            string taskType,
            List<string> availableSkills,
            CancellationToken cancellationToken = default)
        {
            if (taskType?.Equals("NonTechnical", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Result.Success(new List<GeneratedRequiredSkill>());
            }

            // Fix 3: lazy-init — load YAML + build Kernel only once per request lifetime
            if (_cachedFunction == null)
            {
                var promptYaml = await _promptLoader.LoadAsync("Planning/RequiredSkillsEnrichment.yaml");
                _cachedFunction = KernelFunctionYaml.FromPromptYaml(promptYaml);
            }
            if (_cachedKernel == null)
            {
                _cachedKernel = _kernelService.CreateKernel(ModelConstants.CheapModel);
            }
            var function = _cachedFunction;
            var kernel = _cachedKernel;

            var arguments = new KernelArguments
            {
                ["title"] = titleEn,
                ["description"] = descriptionEn,
                ["availableSkills"] = JsonSerializer.Serialize(availableSkills)
            };

            const int maxAttempts = 3;
            string failureReason = "The AI returned no usable required skills.";
            string rawJson = string.Empty;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await kernel.InvokeAsync(function, arguments, cancellationToken);
                    sw.Stop();

                    _telemetry.RecordCall(response.Metadata, sw.ElapsedMilliseconds, "RequiredSkillsEnrichmentAgent", ModelConstants.CheapModel, _logger);

                    rawJson = response.GetValue<string>() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(rawJson))
                    {
                        failureReason = "The AI returned an empty response.";
                        await DelayBeforeRetryAsync(attempt, maxAttempts, cancellationToken);
                        continue;
                    }

                    string jsonToParse = attempt == 1 ? rawJson : TryRepairJsonArray(rawJson);
                    
                    var parsedSkills = TaskPilot.AI.Extensions.AiResponseParser.Parse<List<GeneratedRequiredSkill>>(jsonToParse,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    var validSkills = ValidateSkills(parsedSkills);
                    if (validSkills.Count > 0)
                        return Result.Success(validSkills);

                    failureReason = parsedSkills == null
                        ? "The AI response was not a valid required-skills array."
                        : "The AI returned no valid required skills.";
                    await DelayBeforeRetryAsync(attempt, maxAttempts, cancellationToken);
                }
                catch (JsonException ex)
                {
                    failureReason = "The AI response contained malformed JSON.";
                    _logger.LogWarning(ex, "Required skills JSON parse failed on attempt {Attempt}/{MaxAttempts}.", attempt, maxAttempts);
                    await DelayBeforeRetryAsync(attempt, maxAttempts, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failureReason = $"The AI request failed: {ex.GetType().Name}.";
                    _logger.LogError(ex, "Failed to enrich required skills or parse JSON from AI.");
                    await DelayBeforeRetryAsync(attempt, maxAttempts, cancellationToken);
                }
            }

            _logger.LogWarning("Failed to generate valid skills for task '{Title}' after {Attempts} attempts. Reason={Reason}", titleEn, maxAttempts, failureReason);
            return Result.Failure<List<GeneratedRequiredSkill>>(new Error(
                "REQUIRED_SKILLS_NO_VALID_RESULT",
                ErrorType.Failure,
                failureReason));
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

        private static async Task DelayBeforeRetryAsync(int attempt, int maxAttempts, CancellationToken cancellationToken)
        {
            if (attempt >= maxAttempts)
                return;

            var delayMs = (250 * (1 << (attempt - 1))) + Random.Shared.Next(50, 151);
            await Task.Delay(delayMs, cancellationToken);
        }

        private string TryRepairJsonArray(string raw)
        {
            raw = raw.Trim();
            
            // Remove markdown fences if present
            if (raw.StartsWith("```"))
            {
                var lines = raw.Split('\n').ToList();
                if (lines.Count > 0 && lines[0].StartsWith("```")) lines.RemoveAt(0);
                if (lines.Count > 0 && lines[lines.Count - 1].StartsWith("```")) lines.RemoveAt(lines.Count - 1);
                raw = string.Join('\n', lines).Trim();
            }

            if (!raw.StartsWith("[")) return raw; 
            if (raw.EndsWith("]")) return raw;

            int lastClosingBrace = -1;
            int depth = 0;
            bool inString = false;
            bool escape = false;

            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (escape) { escape = false; continue; }
                if (c == '\\' && inString) { escape = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;

                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) // root level object in the array
                        lastClosingBrace = i;
                }
            }

            if (lastClosingBrace == -1) return "[]"; // could not even parse one object, return empty array

            string repaired = raw.Substring(0, lastClosingBrace + 1) + "\n]";
            return repaired;
        }
    }
}
