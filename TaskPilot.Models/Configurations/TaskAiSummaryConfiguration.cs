using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;

namespace TaskPilot.Models.Configurations
{
    public class TaskAiSummaryConfiguration : IEntityTypeConfiguration<TaskAiSummary>
    {
        public void Configure(EntityTypeBuilder<TaskAiSummary> builder)
        {
            builder.ToTable("TaskAiSummaries");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.ContentEn)
                .IsRequired();

            builder.Property(x => x.ContentAr)
                .IsRequired();

            builder.Property(x => x.CitationsJson)
                .IsRequired();

            builder.Property(x => x.GeneratedAt)
                .IsRequired();

            builder.HasOne(x => x.TaskItem)
                .WithOne(t => t.AiSummary)
                .HasForeignKey<TaskAiSummary>(x => x.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);
                
            builder.HasIndex(x => x.TaskItemId)
                .IsUnique();
        }
    }
}
