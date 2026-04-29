using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Domain.Entities;
using TaskPilot.Infrastructure.Persistence.Configurations.Common;

using Microsoft.EntityFrameworkCore;

namespace TaskPilot.Infrastructure.Persistence.Configurations
{
    public class SubscriptionPlanConfiguration : AuditableEntityConfiguration<SubscriptionPlan, int>
    {
        public override void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
        {
            base.Configure(builder);

            builder.ToTable("SubscriptionPlans");

            builder.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.HasIndex(p => p.Name)
                .IsUnique();

            builder.Property(p => p.MonthlyPrice)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            builder.Property(p => p.AnnualPrice)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            builder.Property(p => p.Currency)
                .IsRequired()
                .HasMaxLength(3)
                .HasDefaultValue("EGP");

            builder.Property(p => p.MaxProjects)
                .IsRequired();

            builder.Property(p => p.MaxUsersPerProject)
                .IsRequired();

            builder.Property(p => p.HasTrial)
                .HasDefaultValue(false);

        }
    }
}
