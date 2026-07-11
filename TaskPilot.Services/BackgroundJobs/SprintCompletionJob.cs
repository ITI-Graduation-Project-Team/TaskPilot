using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskPilot.Data.Repositories;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services.BackgroundJobs
{
    public sealed class SprintCompletionJob(
        IServiceScopeFactory scopeFactory,
        ILogger<SprintCompletionJob> logger)
    {
        [AutomaticRetry(Attempts = 3)]
        public async Task ExecuteAsync(Guid sprintId)
        {
            using var scope = scopeFactory.CreateScope();
            var lifecycleService = scope.ServiceProvider.GetRequiredService<ISprintLifecycleService>();

            if (!await lifecycleService.EnsureCompletedIfDueAsync(sprintId))
            {
                logger.LogInformation("Sprint completion skipped for sprint {SprintId}", sprintId);
                return;
            }

            var retrospectiveService = scope.ServiceProvider.GetRequiredService<ISprintRetrospectiveService>();
            var result = await retrospectiveService.GenerateRetrospectiveAsync(sprintId);

            if (!result.IsSuccess)
                throw new InvalidOperationException($"Could not generate retrospective for sprint {sprintId}");

            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.SaveChangesAsync();

            logger.LogInformation("Sprint {SprintId} was completed and its retrospective is available", sprintId);
        }
    }
}
