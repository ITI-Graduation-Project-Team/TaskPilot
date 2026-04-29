using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Domain.Entities;

public class UserSkillConfiguration : IEntityTypeConfiguration<UserSkill>
{
    public void Configure(EntityTypeBuilder<UserSkill> builder)
    {
        builder.ToTable("UserSkills");

        builder.HasKey(us => new { us.EmployeeId, us.SkillId });

        builder.Property(us => us.Level)
            .IsRequired();

        builder.Property(us => us.YearsOfExperience)
            .IsRequired();

        builder.HasIndex(us => us.SkillId);
    }
}