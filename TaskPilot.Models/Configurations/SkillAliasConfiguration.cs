using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;

namespace TaskPilot.Models.Configurations
{
    public class SkillAliasConfiguration : IEntityTypeConfiguration<SkillAlias>
    {
        public void Configure(EntityTypeBuilder<SkillAlias> builder)
        {
            builder.Property(sa => sa.Alias)
                .HasMaxLength(100);

            builder.HasIndex(sa => sa.Alias)
                .IsUnique();

            builder.HasOne(sa => sa.Skill)
                .WithMany(s => s.Aliases)
                .HasForeignKey(sa => sa.SkillId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
