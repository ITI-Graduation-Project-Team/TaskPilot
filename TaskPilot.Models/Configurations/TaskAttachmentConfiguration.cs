using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Configurations.Common;

namespace TaskPilot.Models.Configurations
{
    public class TaskAttachmentConfiguration
        : AuditableEntityConfiguration<TaskAttachment, Guid>
    {
        public override void Configure(EntityTypeBuilder<TaskAttachment> builder)
        {
            base.Configure(builder);

            builder.ToTable("TaskAttachments");

            builder.Property(a => a.FileUrl)
                .IsRequired()
                .HasMaxLength(2000);

            builder.Property(a => a.PublicId)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(a => a.FileName)
              .IsRequired()
              .HasMaxLength(255);

            builder.Property(a => a.ContentType)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(a => a.FileSize)
                .IsRequired();

            builder.HasOne(a => a.Task)
                .WithMany(t => t.Attachments)
                .HasForeignKey(a => a.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(a => a.TaskId);
            builder.HasIndex(a => new { a.TaskId, a.CreatedAt });
        }
    }
}