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
                .HasMaxLength(3)
                .HasDefaultValue("EGP");

            builder.Property(p => p.Status)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(p => p.PaymentGateway)
                .IsRequired()
                .HasConversion<int>();
                

            builder.Property(p => p.PaymentMethod)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(p => p.GatewayTransactionId)
                .HasMaxLength(200);


            builder.HasOne(p => p.ProjectManager)
                .WithMany(pm => pm.Payments)
                .HasForeignKey(p => p.ProjectManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.Subscription)
                .WithMany(s => s.Payments)
                .HasForeignKey(p => p.UserSubscriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(p => p.GatewayTransactionId)
                .IsUnique()
                .HasFilter("[GatewayTransactionId] IS NOT NULL");

            builder.HasIndex(p => p.ProjectManagerId);
            builder.HasIndex(p => p.UserSubscriptionId);
            builder.HasIndex(p => p.Status);
            builder.HasIndex(p => p.PaymentGateway);
            builder.HasIndex(p => p.PaymentMethod);
        }
    }
}