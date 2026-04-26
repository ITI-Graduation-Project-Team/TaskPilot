using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Domain.Entities;
using TaskPilot.Infrastructure.Persistence.Configurations.Common;

namespace TaskPilot.Infrastructure.Persistence.Configurations
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

            builder.HasOne(us => us.Sprint)
                   .WithMany(s => s.UserStories)
                   .HasForeignKey(us => us.SprintId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(us => us.Tasks)
                   .WithOne()
                   .HasForeignKey(t => t.UserStoryId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
