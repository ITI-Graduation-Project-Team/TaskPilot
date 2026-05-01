using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Configurations.Common;

namespace TaskPilot.Models.Configurations
{
    public class NotificationConfiguration
        : AuditableEntityConfiguration<Notification, Guid>
    {
        public override void Configure(EntityTypeBuilder<Notification> builder)
        {
            base.Configure(builder);

            builder.ToTable("Notifications");

            

            builder.Property(n => n.Type)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(n => n.MessageEn)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(n => n.MessageAr)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(n => n.Url)
                .HasMaxLength(500);

            builder.Property(n => n.IsRead)
                .IsRequired()
                .HasDefaultValue(false);

            builder.HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

          

            builder.HasIndex(n => n.UserId);

            builder.HasIndex(n => new { n.UserId, n.IsRead });

            builder.HasIndex(n => new { n.UserId, n.CreatedAt });
        }
    }
}