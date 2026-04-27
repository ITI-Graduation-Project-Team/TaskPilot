using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;
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
                   .HasMaxLength(4);

            //1 user m payments
            builder.HasOne(x => x.ProjectManager)
                .WithMany(x=>x.Payments)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
             
            //  1 subscription m payments
            builder.HasOne(x => x.Subscription)
                .WithMany(s => s.Payments)
                .HasForeignKey(x => x.UserSubscriptionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
