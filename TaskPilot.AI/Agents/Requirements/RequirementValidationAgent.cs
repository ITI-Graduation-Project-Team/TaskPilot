using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Models.Requirements;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Agents.Requirements
{
    public class RequirementValidationAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;
        private readonly Microsoft.Extensions.Logging.ILogger<RequirementValidationAgent> _logger;
        private readonly ITelemetryAccumulator _telemetry;

        public RequirementValidationAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader,
            Microsoft.Extensions.Logging.ILogger<RequirementValidationAgent> logger,
            ITelemetryAccumulator telemetry)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
            _logger = logger;
            _telemetry = telemetry;
        }

        public async Task<RequirementValidationResult> ValidateAsync(
            RequirementSession session,
            CancellationToken cancellationToken = default)
        {
            var kernel = _kernelService.CreateKernel(ModelConstants.CheapModel);
            var prompt = await _promptLoader.LoadAsync("Requirements/RequirementValidation.yaml");
            var function = KernelFunctionYaml.FromPromptYaml(prompt);

            var arguments = KernelArgumentsFactory.CreateDeterministicArguments();
            arguments["requirements"] = session.Requirements.ToPromptText();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await kernel.InvokeAsync(function, arguments, cancellationToken);
            sw.Stop();

            _telemetry.RecordCall(result.Metadata, sw.ElapsedMilliseconds, "RequirementValidationAgent", ModelConstants.CheapModel, _logger);
            
            var json = result.ToString().Trim();

            try
            {
                var validationResult = JsonSerializer.Deserialize<RequirementValidationResult>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return validationResult ?? new RequirementValidationResult();
            }
            catch
            {
                return new RequirementValidationResult();
            }
        }
    }
}
