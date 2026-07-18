using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Configurations.Common;

namespace TaskPilot.Models.Configurations
{
    public class TaskConfiguration : AuditableEntityConfiguration<TaskItem, Guid>
    {
        public override void Configure(EntityTypeBuilder<TaskItem> builder)
        {
            base.Configure(builder);

            builder.ToTable("Tasks");

            builder.Property(t => t.TitleEn)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(t => t.TitleAr)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(t => t.DescriptionEn)
                .HasMaxLength(2000);

            builder.Property(t => t.DescriptionAr)
                .HasMaxLength(2000);

            builder.Property(t => t.TechnicalSummaryEn)
                .HasMaxLength(2000);

            builder.Property(t => t.TechnicalSummaryAr)
                .HasMaxLength(2000);

            builder.Property(t => t.AcceptanceCriteriaEn)
                .HasMaxLength(2000);

            builder.Property(t => t.AcceptanceCriteriaAr)
                .HasMaxLength(2000);

            builder.Property(t => t.EstimatedHours)
                .HasColumnType("decimal(10,2)")
                .IsRequired();

            builder.Property(t => t.ActualHours)
                .HasColumnType("decimal(10,2)")
                .HasDefaultValue(0);

            builder.Property(t => t.Priority)
                 .IsRequired()
                .HasConversion<int>();

            builder.Property(t => t.Status)
                 .IsRequired()
                .HasConversion<int>();


            builder.HasOne(t => t.Sprint)
                .WithMany(s => s.Tasks)
                .HasForeignKey(t => t.SprintId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            builder.HasOne(t => t.UserStory)
                .WithMany(us => us.Tasks)
                .HasForeignKey(t => t.UserStoryId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(t => t.Employee)
                .WithMany(d => d.AssignedTasks)
                .HasForeignKey(t => t.EmployeeId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasMany(t => t.Comments)
                .WithOne(c => c.Task)
                .HasForeignKey(c => c.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(t => t.Attachments)
                .WithOne(a => a.Task)
                .HasForeignKey(a => a.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(t => t.RequiredSkills)
                .WithOne(rs => rs.Task)
                .HasForeignKey(rs => rs.TaskId)
                .OnDelete(DeleteBehavior.Cascade);


            builder.HasIndex(t => t.SprintId);
            builder.HasIndex(t => t.EmployeeId);
            builder.HasIndex(t => t.UserStoryId);
            builder.HasIndex(t => t.Status);
            builder.HasIndex(t => t.Priority);
        }
    }
}