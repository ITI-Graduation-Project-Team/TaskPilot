using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using TaskPilot.Data.Context;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;
using Hangfire;
using TaskPilot.Services.Helpers;

namespace TaskPilot.Services.BackgroundJobs
{
    public class SprintRiskDetectionJob
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SprintRiskDetectionJob> _logger;

        public SprintRiskDetectionJob(
            IServiceScopeFactory scopeFactory,
            ILogger<SprintRiskDetectionJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        [AutomaticRetry(Attempts = 0)]
        public async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var riskService = scope.ServiceProvider.GetRequiredService<ISprintRiskService>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var activeSprints = await db.Set<Sprint>()
                .Where(s => s.Status == SprintStatus.Active && !s.IsDeleted)
                .Select(s => new { SprintId = s.Id, s.ProjectId, s.Project.ManagerId })
                .ToListAsync(stoppingToken);

            foreach (var sprint in activeSprints)
            {
                using var telemetryContext = AiTelemetryContext.SetContext(
                    sprint.ManagerId,
                    sprint.ProjectId);
                try
                {
                    await riskService.DetectAndPersistRisksAsync(sprint.SprintId, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Risk detection failed for sprint {SprintId}", sprint.SprintId);
                }
            }
        }
    }
}
