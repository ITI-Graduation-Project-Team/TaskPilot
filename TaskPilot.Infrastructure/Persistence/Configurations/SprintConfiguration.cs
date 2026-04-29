using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Domain.Entities;
using TaskPilot.Infrastructure.Persistence.Configurations.Common;

namespace TaskPilot.Infrastructure.Persistence.Configurations
{
    public class SprintConfiguration : AuditableEntityConfiguration<Sprint, Guid>
    {
        public override void Configure(EntityTypeBuilder<Sprint> builder)
        {
            base.Configure(builder);

            builder.ToTable("Sprints");

            builder.Property(s => s.TitleEn)
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(s => s.TitleAr)
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(s => s.SprintGoalEn)
                   .HasMaxLength(1000);

            builder.Property(s => s.SprintGoalAr)
                   .HasMaxLength(1000);

            builder.HasOne(s => s.Project)
                   .WithMany(p => p.Sprints)
                   .HasForeignKey(s => s.ProjectId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(s => s.UserStories)
                   .WithOne(us => us.Sprint)
                   .HasForeignKey(us => us.SprintId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
