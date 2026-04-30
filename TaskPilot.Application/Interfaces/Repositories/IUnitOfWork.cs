using TaskPilot.Domain.Entities;

namespace TaskPilot.Application.Interfaces.Repositories
{
    /// <summary>
    /// Coordinates all repository operations within a single database transaction.
    /// Guarantees that either ALL changes across multiple repositories are persisted
    /// together, or NONE of them are — preventing partial/inconsistent state.
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        // ──────────────────────── Entity Repositories ────────────────────────

        IRepository<User> Users { get; }
        IRepository<Company> Companies { get; }
        IRepository<Project> Projects { get; }
        IRepository<Sprint> Sprints { get; }
        IRepository<UserStory> UserStories { get; }
        IRepository<TaskItem> Tasks { get; }
        IRepository<Notification> Notifications { get; }
        IRepository<Skill> Skills { get; }
        IRepository<UserSkill> UserSkills { get; }
        IRepository<ProjectEmployee> ProjectEmployees { get; }
        IRepository<SubscriptionPlan> SubscriptionPlans { get; }
        IRepository<UserSubscription> UserSubscriptions { get; }
        IRepository<Payment> Payments { get; }
        IRepository<ProjectPolicy> ProjectPolicies { get; }
        IRepository<ProjectManager> ProjectManagers { get; }
        IRepository<TaskAttachment> TaskAttachments { get; }
        IRepository<TaskComment> TaskComments { get; }
        IRepository<TaskRequiredSkill> TaskRequiredSkills { get; }
        IRepository<Employee> Employees { get; }

        // ──────────────────────── Persistence ────────────────────────

        /// <summary>
        /// Persists all tracked changes across every repository to the database.
        /// Returns the number of state entries written.
        /// </summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts an explicit database transaction.
        /// Use when you need to group SaveChangesAsync calls together.
        /// </summary>
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Commits the current transaction.
        /// </summary>
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Rolls back the current transaction, discarding all uncommitted changes.
        /// </summary>
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    }
}
