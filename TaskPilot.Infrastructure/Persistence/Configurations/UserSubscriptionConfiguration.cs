using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using TaskPilot.Domain.Entities;

namespace TaskPilot.Infrastructure.Persistence.Configurations.Common
{
    public class UserSubscriptionConfiguration : AuditableEntityConfiguration<UserSubscription, int>
    {
        public override void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<UserSubscription> builder)
        {
            base.Configure(builder);
            builder.ToTable("UserSubscriptions");
            // 1  subscription plan m user subscription
            builder.HasOne(s => s.Plan)
                .WithMany(p => p.Subscriptions)
                .HasForeignKey(s => s.SubscriptionPlanId)
                .OnDelete(DeleteBehavior.Restrict);



        }
    }
}
