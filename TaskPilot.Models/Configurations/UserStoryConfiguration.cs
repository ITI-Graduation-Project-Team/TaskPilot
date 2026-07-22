using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Models.Configurations.Common;

namespace TaskPilot.Models.Configurations
{
    public class UserStoryConfiguration : AuditableEntityConfiguration<UserStory, Guid>
    {
        public override void Configure(EntityTypeBuilder<UserStory> builder)
        {
            base.Configure(builder);

            builder.ToTable("UserStories");

            builder.Property(us => us.TitleEn)
                   .IsRequired();

            builder.Property(us => us.TitleAr)
                   .IsRequired();

            builder.Property(us => us.Priority)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(s => s.Status)
                     .IsRequired()
                    .HasConversion<int>()
                     .HasDefaultValue(StoryStatus.ToDo);

            builder.HasOne(x => x.Project)
                .WithMany(x => x.UserStories)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Sprint)
                .WithMany(x => x.UserStories)
                .HasForeignKey(x => x.SprintId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            builder.HasIndex(us => us.SprintId);
            builder.HasIndex(us => new { us.SprintId, us.Status });
            builder.HasIndex(us => new { us.SprintId, us.TitleEn })
                 .IsUnique();

           
            builder.HasIndex(us => us.Priority);
        }
    }
}
