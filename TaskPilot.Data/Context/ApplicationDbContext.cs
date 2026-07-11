using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Common;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Data.Context
{
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>, IUnitOfWork
    {
        private readonly ICurrentUserService _currentUserService;
        private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _currentTransaction;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            ICurrentUserService currentUserService)
            : base(options)
        {
            _currentUserService = currentUserService;
        }

        //public DbSet<User> Users => Set<User>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        public DbSet<Company> Companies => Set<Company>();
        public DbSet<Project> Projects => Set<Project>();
        public DbSet<Sprint> Sprints => Set<Sprint>();
        public DbSet<UserStory> UserStories => Set<UserStory>();
        public DbSet<TaskItem> TaskItems => Set<TaskItem>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<Skill> Skills => Set<Skill>();
        public DbSet<UserSkill> UserSkills => Set<UserSkill>();
        public DbSet<ProjectEmployee> ProjectEmployees => Set<ProjectEmployee>();
        public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
        public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<Policy> Policies => Set<Policy>();
        public DbSet<EmployeeInvitation> EmployeeInvitations => Set<EmployeeInvitation>();

        public DbSet<TaskRequiredSkill> TaskRequiredSkills => Set<TaskRequiredSkill>();
        public DbSet<SkillAlias> SkillAliases => Set<SkillAlias>();
        public DbSet<CalenderEvent>CalenderEvents => Set<CalenderEvent>();
        public DbSet<SprintRetrospective> SprintRetrospectives => Set<SprintRetrospective>();
        public DbSet<SprintRiskAlert> SprintRiskAlerts => Set<SprintRiskAlert>();
        public DbSet<TaskAiSummary> TaskAiSummaries => Set<TaskAiSummary>();
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Scan the Models assembly for IEntityTypeConfiguration implementations
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(User).Assembly);//ApplicationDbContext is in TaskPilot.Data, but the entities are in TaskPilot.Models, so we need to specify the assembly of the entities
        }
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var currentUserId = _currentUserService.UserId;

            foreach (var entry in ChangeTracker.Entries<AuditableEntity<Guid>>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.CreatedBy = currentUserId;
                }

                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.ModifiedAt = DateTime.UtcNow;
                    entry.Entity.ModifiedBy = currentUserId;
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }

        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction != null)
                throw new InvalidOperationException("A database transaction is already active.");

            _currentTransaction = await Database.BeginTransactionAsync(cancellationToken);
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_currentTransaction != null)
                    await _currentTransaction.CommitAsync(cancellationToken);
            }
            finally
            {
                if (_currentTransaction != null)
                {
                    await _currentTransaction.DisposeAsync();
                    _currentTransaction = null;
                }
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_currentTransaction != null)
                    await _currentTransaction.RollbackAsync(cancellationToken);
            }
            finally
            {
                if (_currentTransaction != null)
                {
                    await _currentTransaction.DisposeAsync();
                    _currentTransaction = null;
                }
            }
        }
    }
}