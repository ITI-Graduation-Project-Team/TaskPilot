using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;

namespace TaskPilot.Services.BackgroundJobs
{
    public class SubscriptionExpiryJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SubscriptionExpiryJob> _logger;

        public SubscriptionExpiryJob(IServiceScopeFactory scopeFactory, ILogger<SubscriptionExpiryJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Execute immediately on startup, then every 24 hours
            try
            {
                await ProcessExpirationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during initial subscription expiry run");
            }

            using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
            
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ProcessExpirationsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during subscription expiry job");
                }
            }
        }

        private async Task ProcessExpirationsAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRepository<UserSubscription>>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var expiredSubscriptions = await repo.FindAsync(s => s.Status == SubscriptionStatus.Active && s.EndDate < DateTime.UtcNow);

            int count = 0;
            foreach (var sub in expiredSubscriptions)
            {
                sub.Status = SubscriptionStatus.Expired;
                repo.Update(sub);
                count++;
            }

            var stalePendingCutoff = DateTime.UtcNow.AddHours(-2);
            var stalePending = await repo.FindAsync(
                s => s.Status == SubscriptionStatus.Pending &&
                     s.CreatedAt < stalePendingCutoff);

            foreach (var pending in stalePending)
            {
                pending.Status = SubscriptionStatus.Canceled;
                repo.Update(pending);
                count++;
                _logger.LogInformation(
                    "Stale Pending subscription {Id} for user {UserId} canceled after 2-hour TTL",
                    pending.Id, pending.ProjectManagerId);
            }

            if (count > 0)
            {
                await unitOfWork.SaveChangesAsync(stoppingToken);
            }

            _logger.LogInformation("Expired {Count} subscriptions at {Timestamp}", count, DateTime.UtcNow);
        }
    }
}
