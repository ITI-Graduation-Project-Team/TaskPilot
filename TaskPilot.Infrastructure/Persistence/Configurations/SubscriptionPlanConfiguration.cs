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

            builder.Property(p => p.MonthlyPrice)
    .HasColumnType("decimal(18,2)");

            builder.Property(p => p.AnnualPrice)
                .HasColumnType("decimal(18,2)");


        }
    }
}
