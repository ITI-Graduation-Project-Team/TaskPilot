using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskPilot.Data.Context;
using TaskPilot.Models.Entities;

namespace TaskPilot.Services.BackgroundJobs
{
    public class TokenQuotaResetJob
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<TokenQuotaResetJob> _logger;

        public TokenQuotaResetJob(ApplicationDbContext dbContext, ILogger<TokenQuotaResetJob> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("TokenQuotaResetJob started at {Time}", DateTime.UtcNow);

            try
            {
                var rowsAffected = await _dbContext.Set<ProjectManager>()
                    .Where(pm => pm.CurrentTokensUsedThisMonth > 0)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.CurrentTokensUsedThisMonth, 0), cancellationToken);

                _logger.LogInformation("TokenQuotaResetJob completed successfully. Reset token quota for {Count} ProjectManagers.", rowsAffected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while resetting token quotas.");
                throw;
            }
        }
    }
}
