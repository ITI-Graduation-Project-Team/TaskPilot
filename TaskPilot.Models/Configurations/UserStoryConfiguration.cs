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
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(us => us.TitleAr)
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(us => us.DescriptionEn)
                   .HasMaxLength(2000);

            builder.Property(us => us.DescriptionAr)
                   .HasMaxLength(2000);

            builder.Property(us => us.AcceptanceCriteriaEn)
                   .HasMaxLength(2000);

            builder.Property(us => us.AcceptanceCriteriaAr)
                   .HasMaxLength(2000);

            builder.Property(us => us.Priority)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(s => s.Status)
                     .IsRequired()
                    .HasConversion<int>()
                     .HasDefaultValue(StoryStatus.ToDo);

            builder.HasIndex(us => us.SprintId);
            builder.HasIndex(us => new { us.SprintId, us.Status });
            builder.HasIndex(us => new { us.SprintId, us.TitleEn })
                 .IsUnique();

           
            builder.HasIndex(us => us.Priority);
        }
    }
}
