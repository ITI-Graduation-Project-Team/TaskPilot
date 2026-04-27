using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Domain.Entities;
using TaskPilot.Infrastructure.Persistence.Configurations.Common;


namespace TaskPilot.Infrastructure.Persistence.Configurations
{
    public class PaymentConfiguration : AuditableEntityConfiguration<Payment, Guid>
    {
        public override void Configure(EntityTypeBuilder<Payment> builder)
        {
            base.Configure(builder);
            builder.ToTable("Payments");

            builder.Property(p => p.Amount)
                   .IsRequired()
                   .HasColumnType("decimal(18,2)");

            builder.Property(p => p.Currency)
                   .IsRequired()
                   .HasMaxLength(3);

            builder.Property(p => p.Status)
                .IsRequired();

            builder.Property(p => p.PaymentGateway)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(p => p.PaymentMethod)
                .HasMaxLength(50);


            builder.Property(p => p.GatewayTransactionId)
                .HasMaxLength(200);
            builder.HasIndex(p => p.GatewayTransactionId)
                .IsUnique();


            //1 user m payments
            builder.HasOne(x => x.ProjectManager)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);


            //1 user_subscriptions   M  Payments
            builder.HasOne(x => x.Subscription)
                .WithMany(s => s.Payments)
                .HasForeignKey(x => x.UserSubscriptionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
