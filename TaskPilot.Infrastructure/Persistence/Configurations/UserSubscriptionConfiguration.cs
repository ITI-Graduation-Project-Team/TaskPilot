using Microsoft.EntityFrameworkCore;
using TaskPilot.Domain.Entities;
namespace TaskPilot.Infrastructure.Persistence.Configurations.Common
{
    public class UserSubscriptionConfiguration : AuditableEntityConfiguration<UserSubscription, int>
    {
        public override void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<UserSubscription> builder)
        {
            base.Configure(builder);
            builder.ToTable("UserSubscriptions");

            builder.Property(s => s.Status)
           .IsRequired();
            builder.Property(s => s.StartDate)
           .IsRequired();

            builder.Property(s => s.EndDate)
                .IsRequired();
            // 1  subscription_plan m user_subscription
            builder.HasOne(s => s.Plan)
                .WithMany(p => p.Subscriptions)
                .HasForeignKey(s => s.SubscriptionPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            //1 user m user_subscription
            builder.HasOne(s => s.ProjectManager)
                .WithMany(u => u.Subscriptions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);



        }
    }
}
