using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskPilot.Data.Context;
using TaskPilot.Models.Enums;

namespace TaskPilot.Services.BackgroundJobs
{
    public sealed class ProjectKnowledgePromotionJob(
        IServiceScopeFactory scopeFactory,
        ILogger<ProjectKnowledgePromotionJob> logger)
    {
        [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 30, 120 })]
        public async Task ExecuteAsync(Guid projectId)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var documentIds = await db.Projects
                .Where(x => x.Id == projectId && !x.IsDeleted)
                .Select(x => x.DocumentIds)
                .FirstOrDefaultAsync();

            if (documentIds == null || documentIds.Count == 0)
                return;

            var vectorStore = scope.ServiceProvider.GetRequiredService<TaskPilot.AI.Services.Interfaces.IVectorStore>();
            foreach (var documentId in documentIds)
            {
                await vectorStore.PromoteKnowledgeAsync(
                    KnowledgeCollectionType.ProjectPolicies,
                    projectId,
                    documentId);
            }

            logger.LogInformation("Promoted {DocumentCount} documents for project {ProjectId}", documentIds.Count, projectId);
        }
    }
}
