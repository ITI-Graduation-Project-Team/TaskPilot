using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TaskPilot.Domain.Entities;
using TaskPilot.Infrastructure.Identity;

namespace TaskPilot.Infrastructure.Persistence
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();

        public DbSet<Company> Companies => Set<Company>();
        public DbSet<Project> Projects => Set<Project>();
        public DbSet<Sprint> Sprints => Set<Sprint>();

        public DbSet<UserStory> UserStories => Set<UserStory>();
        public DbSet<TaskItem> Tasks => Set<TaskItem>();

        public DbSet<Notification> Notifications => Set<Notification>();

        public DbSet<Skill> Skills => Set<Skill>();
        public DbSet<UserSkill> UserSkills => Set<UserSkill>();

        public DbSet<ProjectEmployee> ProjectEmployees => Set<ProjectEmployee>();

        public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
        public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
        public DbSet<Payment> Payments => Set<Payment>();

        public DbSet<ProjectPolicy> ProjectPolicies => Set<ProjectPolicy>();

     
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }
    }
}