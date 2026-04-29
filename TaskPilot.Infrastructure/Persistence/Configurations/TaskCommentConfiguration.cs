using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Domain.Entities;
using TaskPilot.Infrastructure.Persistence.Configurations.Common;

namespace TaskPilot.Infrastructure.Persistence.Configurations
{
    public class TaskCommentConfiguration
        : AuditableEntityConfiguration<TaskComment, Guid>
    {
        public override void Configure(EntityTypeBuilder<TaskComment> builder)
        {
            base.Configure(builder);

            builder.ToTable("TaskComments");

            builder.Property(tc => tc.ContentEn)
                .IsRequired()
                .HasMaxLength(1000);

            builder.Property(tc => tc.ContentAr)
                .IsRequired()
                .HasMaxLength(1000);

            builder.HasOne(tc => tc.Task)
                .WithMany(t => t.Comments)
                .HasForeignKey(tc => tc.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(tc => tc.User)
                 .WithMany(u => u.Comments)
                 .HasForeignKey(tc => tc.UserId)
                 .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(tc => tc.TaskId);
            builder.HasIndex(tc => tc.UserId);
            builder.HasIndex(tc => new { tc.TaskId, tc.CreatedAt });
        }
    }
}