using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Entities;

namespace TaskPilot.Data.Context
{
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>, IUnitOfWork
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        //public DbSet<User> Users => Set<User>();
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Scan the Models assembly for IEntityTypeConfiguration implementations
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(User).Assembly);//ApplicationDbContext is in TaskPilot.Data, but the entities are in TaskPilot.Models, so we need to specify the assembly of the entities
        }
    }
}