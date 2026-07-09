using System.Text.Json;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Exceptions;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.AI.Models.Planning;
using TaskPilot.Models.Entities;
using Microsoft.Extensions.Logging;

namespace TaskPilot.AI.Agents.Planning
{
    public class WBSGenerationAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;
        private readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        public WBSGenerationAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
        }

        public async Task<GeneratedWbs> GenerateAsync(
            RequirementsSnapshot snapshot,
            System.Collections.Generic.List<string> techStack,
            System.Collections.Generic.List<string> platformTargets,
            string projectType,
            System.Collections.Generic.List<string> availableSkills,
            CancellationToken cancellationToken = default)
        {
            var kernel = _kernelService.CreateKernel(
                ModelConstants.PowerfulModel);

            var prompt = await _promptLoader.LoadAsync(
                "Planning/WbsGeneration.yaml");

            prompt += "\n" +
                      "  CRITICAL: Your response must be a single complete valid JSON object with no trailing text.\n" +
                      "  Do not truncate or cut off the response under any circumstances.\n" +
                      "  Limit each userStory to a maximum of 3 tasks.\n" +
                      "  Limit acceptanceCriteria and acceptanceCriteriaAr arrays to a maximum of 2 items each.\n" +
                      "  Keep acceptanceCriteriaAr values short (under 80 characters each).\n";

            var executionSettings = new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object> { { "max_tokens", 8192 } }
            };

            var arguments = new KernelArguments(executionSettings)
            {
                ["businessRequirements"] =
                    string.Join("\n", snapshot.BusinessRequirements),

                ["technicalRequirements"] =
                    string.Join("\n", snapshot.TechnicalRequirements),

                ["constraints"] =
                    string.Join("\n", snapshot.Constraints),

                ["integrations"] =
                    string.Join("\n", snapshot.Integrations),

                ["scaleRequirements"] =
                    string.Join("\n", snapshot.ScaleRequirements),

                ["techStack"] =
                    techStack != null && techStack.Any()
                        ? string.Join(", ", techStack)
                        : "Not specified — use best practices",

                ["platformTargets"] =
                    platformTargets != null && platformTargets.Any()
                        ? string.Join(", ", platformTargets)
                        : "Web",

                ["projectType"] =
                    !string.IsNullOrEmpty(projectType)
                        ? projectType
                        : "General",

                ["availableSkills"] =
                    availableSkills != null && availableSkills.Any()
                        ? string.Join(", ", availableSkills)
                        : "None"
            };

            const int maxAttempts = 3;
            GeneratedWbs? result = null;
            Exception? lastException = null;
            string rawJson = string.Empty;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    string currentPrompt = prompt;
                    if (attempt == 2)
                    {
                        currentPrompt += "\n  Reduce the number of User Stories. Prioritize JSON completeness over quantity.";
                    }
                    else if (attempt == 3)
                    {
                        currentPrompt += "\n  Generate between 3 and 5 User Stories only. Maximum 2 Tasks per Story. Maximum 2 Required Skills per Task. Return the smallest complete valid JSON possible.";
                    }

                    var function = KernelFunctionYaml.FromPromptYaml(currentPrompt);

                    var invokeResult = await kernel.InvokeAsync(
                        function,
                        arguments,
                        cancellationToken: cancellationToken);

                    rawJson = invokeResult.ToString();
                    
                    int approxTokens = rawJson.Length / 4;
                    _logger.LogInformation("WBS Generation Attempt {Attempt}: Generated Response Length {Length} characters, approx {Tokens} tokens.", attempt, rawJson.Length, approxTokens);

                    string jsonToParse = attempt == 1 ? TryRepairJson(rawJson) : TryRepairJson(rawJson);
                    result = JsonSerializer.Deserialize<GeneratedWbs>(jsonToParse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (result != null) break;
                }
                catch (JsonException ex)
                {
                    lastException = ex;
                    string retryReason = attempt < maxAttempts ? "Reducing size for next attempt" : "Max attempts reached";
                    _logger.LogWarning("WBS JSON parse failed on attempt {Attempt}/3. Reason: {Message}. Retry reason: {RetryReason}. Response length: {Length}", attempt, ex.Message, retryReason, rawJson?.Length ?? 0);
                }
            }

            if (result == null || !result.UserStories.Any())
                throw new WbsGenerationException(
                    $"WBS generation failed after {maxAttempts} attempts due to truncated or invalid JSON. " +
                    "Try reducing the number of requirements or contact support.",
                    lastException?.ToString());

            return result;
        }

        private string TryRepairJson(string raw)
        {
            raw = raw.Trim();
            if (!raw.StartsWith("{")) return raw; // not repairable, return as-is
            if (raw.EndsWith("}")) return raw;    // already complete

            // Find the last fully closed userStory object
            // A closed userStory ends with: "}" followed optionally by whitespace then "," or "]"
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
                    if (depth == 1) // closed a userStory (depth 1 = inside root object)
                        lastClosingBrace = i;
                    else if (depth == 0)
                        return raw; // already balanced
                }
            }

            if (lastClosingBrace == -1) return raw; // cannot repair

            // Truncate after last complete userStory and close the JSON
            string repaired = raw.Substring(0, lastClosingBrace + 1) + "\n  ]\n}";
            return repaired;
        }
    }
}
