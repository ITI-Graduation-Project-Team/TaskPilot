using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Domain.Entities;
using TaskPilot.Infrastructure.Persistence.Configurations.Common;

namespace TaskPilot.Infrastructure.Persistence.Configurations
{
    public class TaskConfiguration : AuditableEntityConfiguration<Domain.Entities.Task, Guid>
    {
        public override void Configure(EntityTypeBuilder<Domain.Entities.Task> builder)
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
                .HasColumnType("decimal(10,2)");

            builder.Property(t => t.ActualHours)
                .HasColumnType("decimal(10,2)");

            builder.Property(t => t.Priority)
                .HasConversion<int>();

            builder.Property(t => t.Status)
                .HasConversion<int>();


            builder.HasOne(t => t.Sprint)
                .WithMany(s => s.Tasks)
                .HasForeignKey(t => t.SprintId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(t => t.UserStory)
                .WithMany(us => us.Tasks)
                .HasForeignKey(t => t.UserStoryId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(t => t.Developer)
                .WithMany(d => d.AssignedTasks)
                .HasForeignKey(t => t.DeveloperId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}