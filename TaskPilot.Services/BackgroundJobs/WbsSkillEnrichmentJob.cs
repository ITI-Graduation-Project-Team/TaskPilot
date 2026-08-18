using Hangfire;
using Hangfire.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskPilot.Data.Context;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;
using TaskPilot.Services.Helpers;

namespace TaskPilot.Services.BackgroundJobs
{
    public sealed class WbsSkillEnrichmentJob(
        IServiceScopeFactory scopeFactory,
        ILogger<WbsSkillEnrichmentJob> logger)
    {
        [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 30, 120 })]
        public async Task ExecuteAsync(Guid projectId, Guid initiatedByUserId, PerformContext context)
        {
            using var telemetryContext = AiTelemetryContext.SetContext(initiatedByUserId, projectId);
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var state = await db.ProjectSetupStates.FirstOrDefaultAsync(x => x.ProjectId == projectId)
                    ?? throw new InvalidOperationException("Project setup state was not found.");

                if (state.SkillsStatus == BackgroundSetupStatus.Succeeded && state.TasksSkipped == 0)
                    return;
                if (state.SkillsStatus == BackgroundSetupStatus.Running
                    && !string.Equals(state.SkillsJobId, context?.BackgroundJob?.Id, StringComparison.Ordinal))
                    return;

                state.SkillsStatus = BackgroundSetupStatus.Running;
                state.SkillsAttemptCount++;
                state.SkillsStartedAt = DateTime.UtcNow;
                state.SkillsError = null;
                await db.SaveChangesAsync();

                var enrichment = scope.ServiceProvider.GetRequiredService<IWbsSkillEnrichmentService>();
                var result = await enrichment.EnrichProjectTasksAsync(projectId);
                if (result.IsFailure)
                    throw new InvalidOperationException(result.Error.Description ?? result.Error.Code);

                state.SkillsStatus = result.Value.TasksSkipped == 0
                    ? BackgroundSetupStatus.Succeeded
                    : result.Value.TasksEnriched > 0
                        ? BackgroundSetupStatus.PartiallySucceeded
                        : BackgroundSetupStatus.Failed;
                state.TasksProcessed = result.Value.TasksProcessed;
                state.TasksEnriched = result.Value.TasksEnriched;
                state.TasksSkipped = result.Value.TasksSkipped;
                state.SkillsCreated = result.Value.SkillsCreated;
                state.SkillsCompletedAt = DateTime.UtcNow;
                state.SkillsError = result.Value.TasksSkipped > 0
                    ? $"{result.Value.TasksSkipped} technical task(s) still need required skills. "
                        + string.Join(" ", result.Value.Warnings.Take(5))
                    : null;
                await db.SaveChangesAsync();

                var project = await db.Projects.AsNoTracking().FirstAsync(x => x.Id == projectId);
                try
                {
                    var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    await notifications.SendAsync(project.ManagerId, NotificationType.ProjectSetupCompleted,
                        $"{project.NameEn} is ready. Open the backlog to continue.",
                        $"المشروع {project.NameAr} جاهز. افتح قائمة العمل للمتابعة.",
                        $"/dashboard/projects/{projectId}/setup");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Setup completed but its notification failed for project {ProjectId}", projectId);
                }
            }
            catch (Exception ex)
            {
                var retryCount = context?.GetJobParameter<int>("RetryCount") ?? 0;
                await RecordFailureAsync(projectId, ex.Message, retryCount >= 2);
                throw;
            }
        }

        private async Task RecordFailureAsync(Guid projectId, string message, bool finalAttempt)
        {
            try
            {
                using var failureScope = scopeFactory.CreateScope();
                var db = failureScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var state = await db.ProjectSetupStates.FirstOrDefaultAsync(x => x.ProjectId == projectId);
                if (state == null || (state.SkillsStatus == BackgroundSetupStatus.Succeeded && state.TasksSkipped == 0)) return;
                state.SkillsStatus = finalAttempt ? BackgroundSetupStatus.Failed : BackgroundSetupStatus.Queued;
                state.SkillsError = finalAttempt ? message : "Skill enrichment is being retried automatically.";
                await db.SaveChangesAsync();
            }
            catch (Exception recordEx)
            {
                logger.LogError(recordEx, "Could not persist skill enrichment failure for project {ProjectId}", projectId);
            }
        }
    }
}
