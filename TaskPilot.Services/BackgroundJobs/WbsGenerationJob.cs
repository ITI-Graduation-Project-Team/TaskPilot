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
                if (state == null || state.WbsStatus == BackgroundSetupStatus.Succeeded) return;
                state.WbsStatus = finalAttempt ? BackgroundSetupStatus.Failed : BackgroundSetupStatus.Queued;
                state.WbsError = finalAttempt ? message : "Generation is being retried automatically.";
                await db.SaveChangesAsync();

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
    }
}
