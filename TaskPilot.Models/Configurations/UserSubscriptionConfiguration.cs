using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Configurations.Common;

public class UserSubscriptionConfiguration
    : AuditableEntityConfiguration<UserSubscription, Guid>
{
    public override void Configure(EntityTypeBuilder<UserSubscription> builder)
    {
        base.Configure(builder);

        builder.ToTable("UserSubscriptions");

        builder.Property(s => s.StartDate)
            .IsRequired();

        builder.Property(s => s.EndDate)
            .IsRequired();

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(s => s.BillingCycle)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(s => s.Gateway)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(s => s.IsTrial)
            .HasDefaultValue(false);

        builder.Property(s => s.AutoRenew)
            .HasDefaultValue(true);

        builder.HasOne(s => s.Plan)
            .WithMany(p => p.Subscriptions)
            .HasForeignKey(s => s.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ProjectManager)
            .WithMany(pm => pm.Subscriptions)
            .HasForeignKey(s => s.ProjectManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.ProjectManagerId);
        builder.HasIndex(s => s.SubscriptionPlanId);
        builder.HasIndex(s => s.Status);

        builder.Property(s => s.GatewaySubscriptionId)
            .HasMaxLength(255);
            
        builder.Property(s => s.GatewayCustomerId)
            .HasMaxLength(255);

        builder.HasIndex(s => s.GatewaySubscriptionId)
            .IsUnique()
            .HasFilter("[GatewaySubscriptionId] IS NOT NULL");
    }
}