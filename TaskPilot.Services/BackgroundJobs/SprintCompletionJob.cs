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

            var sprintRepo = scope.ServiceProvider.GetRequiredService<IRepository<TaskPilot.Models.Entities.Sprint>>();
            var sprint = await sprintRepo.GetByIdAsync(sprintId);
            if (sprint == null) throw new InvalidOperationException("Sprint not found");

            var retrospectiveService = scope.ServiceProvider.GetRequiredService<ISprintRetrospectiveService>();
            var result = await retrospectiveService.GenerateAsync(sprint.ProjectId, sprintId, "English");

            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.SaveChangesAsync();

            logger.LogInformation("Sprint {SprintId} was completed and its retrospective is available", sprintId);
        }
    }
}
