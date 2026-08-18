using Microsoft.SemanticKernel;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TaskPilot.Services.Interfaces;
using TaskPilot.AI.Services;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.Services.Filters
{
    public class AiTelemetryFilter : IFunctionInvocationFilter
    {
        private readonly IAiUsageRecorder _usageRecorder;

        public AiTelemetryFilter(IAiUsageRecorder usageRecorder)
        {
            _usageRecorder = usageRecorder;
        }

        public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
        {
            // Only track prompt/LLM calls, skip confirm update logic
            if (context.Function.Name == "ConfirmBacklogUpdates")
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

                Guid? projectId = null;
                if (context.Arguments != null)
                {
                    foreach (var arg in context.Arguments)
                    {
                        if (arg.Key.Equals("projectId", StringComparison.OrdinalIgnoreCase) &&
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

                var descriptor = context.Kernel.Services.GetService(typeof(AiKernelModelDescriptor))
                    as AiKernelModelDescriptor;
                var hasProviderUsage = context.Result?.Metadata?.ContainsKey("Usage") == true;
                if (descriptor != null && (hasProviderUsage || status == "Failed"))
                {
                    await _usageRecorder.RecordFromMetadataAsync(
                        context.Result?.Metadata,
                        context.Function.Name,
                        descriptor.ModelId,
                        elapsedMs,
                        status,
                        errorMessage,
                        projectId,
                        CancellationToken.None);
                }
            }
        }
    }
}
