using Microsoft.SemanticKernel;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TaskPilot.Services.Interfaces;
using TaskPilot.AI.Constants;

using Microsoft.EntityFrameworkCore;
using TaskPilot.Models.Entities;
using TaskPilot.Data.Context;

namespace TaskPilot.Services.Filters
{
    public class AiTelemetryFilter : IFunctionInvocationFilter
    {
        private readonly IAiTelemetryService _telemetryService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITokenQuotaContext _tokenQuotaContext;
        private readonly ApplicationDbContext _dbContext;

        public AiTelemetryFilter(
            IAiTelemetryService telemetryService, 
            ICurrentUserService currentUserService,
            ITokenQuotaContext tokenQuotaContext,
            ApplicationDbContext dbContext)
        {
            _telemetryService = telemetryService;
            _currentUserService = currentUserService;
            _tokenQuotaContext = tokenQuotaContext;
            _dbContext = dbContext;
        }

        public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
        {
            // Only track prompt/LLM calls, skip confirm update logic
            if (context.Function.Metadata == null || context.Function.Name == "ConfirmBacklogUpdates")
            {
                await next(context);
                return;
            }

            // 1. Extract ProjectId early
            Guid? projectId = null;
            if (context.Arguments != null)
            {
                foreach (var arg in context.Arguments)
                {
                    if ((arg.Key.Equals("projectId", StringComparison.OrdinalIgnoreCase)) && 
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

            Guid? projectManagerId = null;

            if (projectId.HasValue && projectId.Value != Guid.Empty)
            {
                var project = await _dbContext.Projects
                    .Where(p => p.Id == projectId.Value)
                    .Select(p => new { p.ManagerId })
                    .FirstOrDefaultAsync();

                if (project != null)
                {
                    projectManagerId = project.ManagerId;
                }
            }
            else
            {
                var currentUserId = _currentUserService.UserId;
                if (currentUserId.HasValue)
                {
                    var user = await _dbContext.Users.Include(u => u.Company).FirstOrDefaultAsync(u => u.Id == currentUserId.Value);
                    if (user is ProjectManager)
                    {
                        projectManagerId = user.Id;
                    }
                    else if (user?.Company != null)
                    {
                        projectManagerId = user.Company.OwnerId;
                    }
                }
            }

            if (projectManagerId.HasValue)
            {
                var pm = await _dbContext.Set<ProjectManager>()
                    .Where(pm => pm.Id == projectManagerId.Value)
                    .FirstOrDefaultAsync();
                    
                var pmUsage = pm?.CurrentTokensUsedThisMonth ?? 0;

                var activeSubscription = await _dbContext.UserSubscriptions
                    .Include(s => s.Plan)
                    .Where(s => s.ProjectManagerId == projectManagerId.Value && s.Status == TaskPilot.Models.Enums.SubscriptionStatus.Active)
                    .FirstOrDefaultAsync();
                    
                var maxTokens = activeSubscription?.Plan?.MaxTokensPerMonth ?? 0;

                if (pmUsage >= maxTokens)
                {
                    Console.WriteLine($"[AiTelemetryFilter] LIMIT REACHED. Usage: {pmUsage}, Max: {maxTokens}. Context Hash: {_tokenQuotaContext.GetHashCode()}");
                    _tokenQuotaContext.LimitReached = true;
                    _tokenQuotaContext.CurrentUsage = pmUsage;
                    _tokenQuotaContext.Limit = maxTokens;
                    
                    return; // Short-circuit
                }
                else
                {
                    Console.WriteLine($"[AiTelemetryFilter] Usage OK. Usage: {pmUsage}, Max: {maxTokens}. Context Hash: {_tokenQuotaContext.GetHashCode()}");
                }
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

                // Token increment logic
                if (status == "Success" && projectManagerId.HasValue && (promptTokens > 0 || completionTokens > 0))
                {
                    var totalTokens = promptTokens + completionTokens;
                    await _dbContext.Set<ProjectManager>()
                        .Where(pm => pm.Id == projectManagerId.Value)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.CurrentTokensUsedThisMonth, p => p.CurrentTokensUsedThisMonth + totalTokens));
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
