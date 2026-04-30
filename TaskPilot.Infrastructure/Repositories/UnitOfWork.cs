using Microsoft.EntityFrameworkCore.Storage;
using TaskPilot.Application.Interfaces.Repositories;
using TaskPilot.Domain.Entities;
using TaskPilot.Infrastructure.Persistence;

namespace TaskPilot.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private IDbContextTransaction? _currentTransaction;

        // ── Lazy-initialized repository backing fields ──
        private IRepository<User>? _users;
        private IRepository<Company>? _companies;
        private IRepository<Project>? _projects;
        private IRepository<Sprint>? _sprints;
        private IRepository<UserStory>? _userStories;
        private IRepository<TaskItem>? _tasks;
        private IRepository<Notification>? _notifications;
        private IRepository<Skill>? _skills;
        private IRepository<UserSkill>? _userSkills;
        private IRepository<ProjectEmployee>? _projectEmployees;
        private IRepository<SubscriptionPlan>? _subscriptionPlans;
        private IRepository<UserSubscription>? _userSubscriptions;
        private IRepository<Payment>? _payments;
        private IRepository<ProjectPolicy>? _projectPolicies;
        private IRepository<ProjectManager>? _projectManagers;
        private IRepository<TaskAttachment>? _taskAttachments;
        private IRepository<TaskComment>? _taskComments;
        private IRepository<TaskRequiredSkill>? _taskRequiredSkills;
        private IRepository<Employee>? _employees;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        // ──────────────────────── Entity Repositories ────────────────────────
        // Lazy initialization ensures repositories are created only when first accessed,
        // and they all share the same DbContext instance for transactional consistency.

        public IRepository<User> Users
            => _users ??= new Repository<User>(_context);

        public IRepository<Company> Companies
            => _companies ??= new Repository<Company>(_context);

        public IRepository<Project> Projects
            => _projects ??= new Repository<Project>(_context);

        public IRepository<Sprint> Sprints
            => _sprints ??= new Repository<Sprint>(_context);

        public IRepository<UserStory> UserStories
            => _userStories ??= new Repository<UserStory>(_context);

        public IRepository<TaskItem> Tasks
            => _tasks ??= new Repository<TaskItem>(_context);

        public IRepository<Notification> Notifications
            => _notifications ??= new Repository<Notification>(_context);

        public IRepository<Skill> Skills
            => _skills ??= new Repository<Skill>(_context);

        public IRepository<UserSkill> UserSkills
            => _userSkills ??= new Repository<UserSkill>(_context);

        public IRepository<ProjectEmployee> ProjectEmployees
            => _projectEmployees ??= new Repository<ProjectEmployee>(_context);

        public IRepository<SubscriptionPlan> SubscriptionPlans
            => _subscriptionPlans ??= new Repository<SubscriptionPlan>(_context);

        public IRepository<UserSubscription> UserSubscriptions
            => _userSubscriptions ??= new Repository<UserSubscription>(_context);

        public IRepository<Payment> Payments
            => _payments ??= new Repository<Payment>(_context);

        public IRepository<ProjectPolicy> ProjectPolicies
            => _projectPolicies ??= new Repository<ProjectPolicy>(_context);

        public IRepository<ProjectManager> ProjectManagers
            => _projectManagers ??= new Repository<ProjectManager>(_context);

        public IRepository<TaskAttachment> TaskAttachments
            => _taskAttachments ??= new Repository<TaskAttachment>(_context);

        public IRepository<TaskComment> TaskComments
            => _taskComments ??= new Repository<TaskComment>(_context);

        public IRepository<TaskRequiredSkill> TaskRequiredSkills
            => _taskRequiredSkills ??= new Repository<TaskRequiredSkill>(_context);

        public IRepository<Employee> Employees
            => _employees ??= new Repository<Employee>(_context);

        // ──────────────────────── Persistence ────────────────────────

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }

        // ──────────────────────── Transaction Management ────────────────────────

        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            _currentTransaction ??= await _context.Database.BeginTransactionAsync(cancellationToken);
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction is null)
                throw new InvalidOperationException("No active transaction to commit. Call BeginTransactionAsync first.");

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                await _currentTransaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await RollbackTransactionAsync(cancellationToken);
                throw;
            }
            finally
            {
                await DisposeTransactionAsync();
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction is null)
                return;

            try
            {
                await _currentTransaction.RollbackAsync(cancellationToken);
            }
            finally
            {
                await DisposeTransactionAsync();
            }
        }

        // ──────────────────────── Dispose ────────────────────────

        private async Task DisposeTransactionAsync()
        {
            if (_currentTransaction is not null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }

        public void Dispose()
        {
            _currentTransaction?.Dispose();
            _context.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
