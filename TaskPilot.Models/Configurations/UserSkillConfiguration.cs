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

        builder.HasIndex(us => new
        {
            us.UserId,
            us.SkillId
        }).IsUnique();

        builder.HasIndex(us => us.SkillId);

        builder.HasIndex(us => new
        {
            us.SkillId,
            us.Level
        });

        builder.HasOne(us => us.User)
            .WithMany(u => u.UserSkills)
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(us => us.Level)
            .HasConversion<string>();

        builder.Property(us => us.IsPrimary)
            .HasDefaultValue(false);

        builder.Property(us => us.ConfidenceScore)
            .HasPrecision(5, 2);
    }
}