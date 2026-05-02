using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Configurations.Common;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;

public class UserSkillConfiguration
    : AuditableEntityConfiguration<UserSkill, Guid>
{
    public override void Configure(EntityTypeBuilder<UserSkill> builder)
    {
        base.Configure(builder);

        builder.ToTable("UserSkills");

        builder.HasIndex(us => new { us.UserId, us.SkillId })
               .IsUnique();

        builder.HasOne(us => us.User)
            .WithMany(u => u.UserSkills)
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);


        builder.Property(us => us.Level)
            .HasDefaultValue(SkillLevel.Intermediate);

        builder.HasIndex(us => us.SkillId);
    }
}