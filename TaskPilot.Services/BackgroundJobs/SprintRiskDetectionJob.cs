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

            var activeSprintIds = await db.Set<Sprint>()
                .Where(s => s.Status == SprintStatus.Active && !s.IsDeleted)
                .Select(s => s.Id)
                .ToListAsync(stoppingToken);

            foreach (var sprintId in activeSprintIds)
            {
                try
                {
                    await riskService.DetectAndPersistRisksAsync(sprintId, stoppingToken);
                    await riskService.AnalyzeSprintBurnoutAsync(sprintId, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Risk detection failed for sprint {SprintId}", sprintId);
                }
            }
        }
    }
}
