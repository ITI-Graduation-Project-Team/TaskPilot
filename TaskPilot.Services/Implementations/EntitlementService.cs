using Microsoft.EntityFrameworkCore;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services.Implementations
{
    public class EntitlementService : IEntitlementService
    {
        private readonly IRepository<UserSubscription> _subscriptionRepo;
        private readonly IRepository<Project> _projectRepo;
        private readonly IRepository<ProjectEmployee> _projectEmployeeRepo;
        private readonly IRepository<ProjectManager> _pmRepo;

        public EntitlementService(
            IRepository<UserSubscription> subscriptionRepo,
            IRepository<Project> projectRepo,
            IRepository<ProjectEmployee> projectEmployeeRepo,
            IRepository<ProjectManager> pmRepo)
        {
            _subscriptionRepo = subscriptionRepo;
            _projectRepo = projectRepo;
            _projectEmployeeRepo = projectEmployeeRepo;
            _pmRepo = pmRepo;
        }

        public async Task<Result> EnsureCanCreateProjectAsync(Guid managerId, CancellationToken cancellationToken = default)
        {
            var activeSubscription = await _subscriptionRepo.GetQueryable()
                .Include(s => s.Plan)
                .Where(s => s.ProjectManagerId == managerId && 
                            s.Status == SubscriptionStatus.Active && 
                            s.EndDate >= DateTime.UtcNow)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeSubscription == null || activeSubscription.Plan == null)
            {
                return Result.Failure(CommonErrors.NoActiveSubscription());
            }

            var plan = activeSubscription.Plan;

            var activeProjectCount = await _projectRepo.GetQueryable()
                .Where(p => p.ManagerId == managerId && 
                            !p.IsDeleted &&
                            (p.Status == ProjectStatus.Draft || p.Status == ProjectStatus.Active))
                .CountAsync(cancellationToken);

            if (activeProjectCount >= plan.MaxProjects)
            {
                return Result.Failure(CommonErrors.MaxProjectsLimitReached(plan.MaxProjects, activeProjectCount));
            }

            return Result.Success();
        }

        public async Task<Result> EnsureCanAddTeamMembersAsync(Guid projectId, int countToAdd, CancellationToken cancellationToken = default)
        {
            var project = await _projectRepo.GetByIdAsync(projectId);
            if (project == null)
            {
                return Result.Failure(CommonErrors.NotFound("Project"));
            }

            var managerId = project.ManagerId;

            var activeSubscription = await _subscriptionRepo.GetQueryable()
                .Include(s => s.Plan)
                .Where(s => s.ProjectManagerId == managerId && 
                            s.Status == SubscriptionStatus.Active && 
                            s.EndDate >= DateTime.UtcNow)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeSubscription == null || activeSubscription.Plan == null)
            {
                return Result.Failure(CommonErrors.NoActiveSubscription());
            }

            var plan = activeSubscription.Plan;

            var currentTeamCount = await _projectEmployeeRepo.GetQueryable()
                .Where(pe => pe.ProjectId == projectId)
                .CountAsync(cancellationToken);

            if (currentTeamCount + countToAdd > plan.MaxUsersPerProject)
            {
                return Result.Failure(CommonErrors.MaxTeamMembersLimitReached(plan.MaxUsersPerProject, currentTeamCount));
            }

            return Result.Success();
        }

        public async Task<Result> EnsureCanUploadAsync(Guid ownerPmId, long incomingFileSizeBytes, long existingFileSizeBytesBeingReplaced = 0, CancellationToken cancellationToken = default)
        {
            var activeSubscription = await _subscriptionRepo.GetQueryable()
                .Include(s => s.Plan)
                .Where(s => s.ProjectManagerId == ownerPmId && 
                            s.Status == SubscriptionStatus.Active && 
                            s.EndDate >= DateTime.UtcNow)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeSubscription == null || activeSubscription.Plan == null)
            {
                return Result.Failure(CommonErrors.NoActiveSubscription());
            }

            var pm = await _pmRepo.GetByIdAsync(ownerPmId);
            if (pm == null)
            {
                return Result.Failure(CommonErrors.NotFound("ProjectManager"));
            }

            var plan = activeSubscription.Plan;

            long newTotalBytes = pm.CurrentStorageUsedBytes - existingFileSizeBytesBeingReplaced + incomingFileSizeBytes;
            long maxBytes = (long)plan.MaxStorageMb * 1024 * 1024;

            if (newTotalBytes > maxBytes)
            {
                return Result.Failure(CommonErrors.StorageLimitReached(plan.MaxStorageMb, (double)newTotalBytes / (1024 * 1024)));
            }

            return Result.Success();
        }

        public async Task UpdateStorageUsageAsync(Guid ownerPmId, long netBytesChange, CancellationToken cancellationToken = default)
        {
            await _pmRepo.GetQueryable()
                .Where(p => p.Id == ownerPmId)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    p => p.CurrentStorageUsedBytes,
                    p => p.CurrentStorageUsedBytes + netBytesChange < 0 
                        ? 0 
                        : p.CurrentStorageUsedBytes + netBytesChange), 
                    cancellationToken);
        }
    }
}
