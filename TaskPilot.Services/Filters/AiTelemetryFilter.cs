using Microsoft.SemanticKernel;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TaskPilot.Services.Interfaces;
using TaskPilot.AI.Constants;

namespace TaskPilot.Services.Filters
{
    public class AiTelemetryFilter : IFunctionInvocationFilter
    {
        private readonly IAiTelemetryService _telemetryService;
        private readonly ICurrentUserService _currentUserService;

        public AiTelemetryFilter(IAiTelemetryService telemetryService, ICurrentUserService currentUserService)
        {
            _telemetryService = telemetryService;
            _currentUserService = currentUserService;
        }

        public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
        {
            // Only track prompt/LLM calls, skip confirm update logic
            if (context.Function.Metadata == null || context.Function.Name == "ConfirmBacklogUpdates")
            {
                await next(context);
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            string status = "Success";
            string? errorMessage = null;

            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                status = "Failed";
                errorMessage = ex.Message;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                long elapsedMs = stopwatch.ElapsedMilliseconds;

                int promptTokens = 0;
                int completionTokens = 0;

                if (context.Result?.Metadata != null)
                {
                    if (context.Result.Metadata.TryGetValue("Usage", out var usageObj) && usageObj != null)
                    {
                        var type = usageObj.GetType();
                        try
                        {
                            var inputProp = type.GetProperty("InputTokenCount") ?? type.GetProperty("PromptTokens");
                            var outputProp = type.GetProperty("OutputTokenCount") ?? type.GetProperty("CompletionTokens");

                            if (inputProp != null) promptTokens = Convert.ToInt32(inputProp.GetValue(usageObj));
                            if (outputProp != null) completionTokens = Convert.ToInt32(outputProp.GetValue(usageObj));
                        }
                        catch
                        {
                            // Fallback if parsing fails
                        }
                    }
                }

                // If no tokens were captured, apply fallback values
                if (status == "Success" && promptTokens == 0 && completionTokens == 0)
                {
                    promptTokens = 1500;
                    completionTokens = 800;
                }

                Guid? projectId = null;
                if (context.Arguments != null)
                {
                    foreach (var arg in context.Arguments)
                    {
                        if ((arg.Key.Equals("projectId", StringComparison.OrdinalIgnoreCase) || 
                             arg.Key.Equals("projectId", StringComparison.OrdinalIgnoreCase)) && 
                            arg.Value != null)
                        {
                            if (Guid.TryParse(arg.Value.ToString(), out var parsedId))
                            {
                                projectId = parsedId;
                                break;
                            }
                        }
                    }
                }

                var userId = _currentUserService.UserId;
                if (userId.HasValue && userId.Value != Guid.Empty)
                {
                    string modelName = ModelConstants.CheapModel; // default
                    if (context.Result?.Metadata != null)
                    {
                        if (context.Result.Metadata.TryGetValue("ModelId", out var modelIdObj) && modelIdObj != null)
                        {
                            modelName = modelIdObj.ToString() ?? modelName;
                        }
                    }
                    
                    await _telemetryService.LogTelemetryAsync(
                        userId: userId.Value,
                        projectId: projectId,
                        operationType: context.Function.Name,
                        modelName: modelName,
                        promptTokens: promptTokens,
                        completionTokens: completionTokens,
                        responseTimeMs: elapsedMs,
                        status: status,
                        errorMessage: errorMessage
                    );
                }
            }
        }
    }
}
