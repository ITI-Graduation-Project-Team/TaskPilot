using Hangfire;
using Hangfire.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskPilot.Data.Context;
using TaskPilot.DTOs.Projects;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;
using TaskPilot.Services.Helpers;

namespace TaskPilot.Services.BackgroundJobs
{
    public sealed class WbsGenerationJob(
        IServiceScopeFactory scopeFactory,
        IBackgroundJobClient jobs,
        ILogger<WbsGenerationJob> logger)
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

                if (state.WbsStatus == BackgroundSetupStatus.Succeeded)
                    return;
                if (state.WbsStatus == BackgroundSetupStatus.Running
                    && !string.Equals(state.WbsJobId, context?.BackgroundJob?.Id, StringComparison.Ordinal))
                    return;

                state.WbsStatus = BackgroundSetupStatus.Running;
                state.WbsAttemptCount++;
                state.WbsStartedAt = DateTime.UtcNow;
                state.WbsError = null;
                await db.SaveChangesAsync();
                await NotifyStatusChangedAsync(scope.ServiceProvider, initiatedByUserId, projectId,
                    "Wbs", state.WbsStatus);

                var generator = scope.ServiceProvider.GetRequiredService<IWbsGenerationService>();
                var result = await generator.GenerateAsync(projectId);
                if (result.IsFailure)
                    throw new InvalidOperationException(result.Error.Description ?? result.Error.Code);

                state.WbsStatus = BackgroundSetupStatus.Succeeded;
                state.UserStoriesCreated = result.Value.UserStoriesCreated;
                state.TasksCreated = result.Value.TasksCreated;
                state.WbsCompletedAt = DateTime.UtcNow;
                state.WbsError = null;
                state.SkillsStatus = BackgroundSetupStatus.Queued;
                await db.SaveChangesAsync();
                await NotifyStatusChangedAsync(scope.ServiceProvider, initiatedByUserId, projectId,
                    "Wbs", state.WbsStatus);
                await NotifyStatusChangedAsync(scope.ServiceProvider, initiatedByUserId, projectId,
                    "SkillEnrichment", state.SkillsStatus);

                state.SkillsJobId = jobs.Enqueue<WbsSkillEnrichmentJob>(job =>
                    job.ExecuteAsync(projectId, initiatedByUserId, null!));
                await db.SaveChangesAsync();

                try
                {
                    var project = await db.Projects.AsNoTracking().FirstAsync(x => x.Id == projectId);
                    var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    await notifications.SendAsync(project.ManagerId, NotificationType.BacklogGenerated,
                        $"The backlog for {project.NameEn} is ready.",
                        $"قائمة العمل للمشروع {project.NameAr} جاهزة.",
                        $"/dashboard/projects/{projectId}/setup");
                }
                catch (Exception notificationEx)
                {
                    logger.LogWarning(notificationEx, "WBS completed but its notification failed for project {ProjectId}", projectId);
                }
                logger.LogInformation("WBS generation completed for project {ProjectId}", projectId);
            }
            catch (Exception ex)
            {
                var retryCount = context?.GetJobParameter<int>("RetryCount") ?? 0;
                await RecordFailureAsync(projectId, initiatedByUserId, ex.Message, retryCount >= 2);
                throw;
            }
        }

        private async Task RecordFailureAsync(
            Guid projectId,
            Guid initiatedByUserId,
            string message,
            bool finalAttempt)
        {
            try
            {
                using var failureScope = scopeFactory.CreateScope();
                var db = failureScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var state = await db.ProjectSetupStates.FirstOrDefaultAsync(x => x.ProjectId == projectId);
                if (state == null || state.WbsStatus == BackgroundSetupStatus.Succeeded) return;
                state.WbsStatus = finalAttempt ? BackgroundSetupStatus.Failed : BackgroundSetupStatus.Queued;
                state.WbsError = finalAttempt ? message : "Generation is being retried automatically.";
                await db.SaveChangesAsync();
                await NotifyStatusChangedAsync(failureScope.ServiceProvider, initiatedByUserId, projectId,
                    "Wbs", state.WbsStatus);

                if (finalAttempt)
                {
                    var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projectId);
                    if (project != null)
                    {
                        var notifications = failureScope.ServiceProvider.GetRequiredService<INotificationService>();
                        await notifications.SendAsync(project.ManagerId, NotificationType.ProjectSetupFailed,
                            $"WBS generation failed for {project.NameEn}.",
                            $"فشل إنشاء هيكل العمل للمشروع {project.NameAr}.",
                            $"/dashboard/projects/{projectId}/setup");
                    }
                }
            }
            catch (Exception notificationEx)
            {
                logger.LogError(notificationEx, "Could not persist WBS failure state for project {ProjectId}", projectId);
            }
        }

        private async Task NotifyStatusChangedAsync(
            IServiceProvider services,
            Guid userId,
            Guid projectId,
            string stage,
            BackgroundSetupStatus status)
        {
            try
            {
                var notifier = services.GetRequiredService<IProjectSetupStatusNotifier>();
                await notifier.NotifyAsync(userId, new ProjectSetupStatusChangedDto
                {
                    ProjectId = projectId,
                    Stage = stage,
                    Status = status.ToString(),
                    OccurredAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Could not broadcast project setup status {Stage}/{Status} for project {ProjectId}",
                    stage, status, projectId);
            }
        }
    }
}
