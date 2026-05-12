using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;

public class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("Skills");

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.NormalizedName)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(s => s.NormalizedName)
            .IsUnique();

        builder.HasIndex(s => s.Name);

        builder.HasMany(s => s.UserSkills)
            .WithOne(us => us.Skill)
            .HasForeignKey(us => us.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}