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
            // مهم جدًا
            base.Configure(builder);

            builder.ToTable("TaskComments");

            // Properties
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

            builder.Property(tc => tc.UserId)
                .IsRequired(false);

            builder.HasIndex(tc => tc.TaskId);
            builder.HasIndex(tc => tc.UserId);
        }
    }
}