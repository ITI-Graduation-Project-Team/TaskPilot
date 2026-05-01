using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;

namespace TaskPilot.Models.Configurations
{
    public class TaskRequiredSkillConfiguration
        : IEntityTypeConfiguration<TaskRequiredSkill>
    {
        public void Configure(EntityTypeBuilder<TaskRequiredSkill> builder)
        {
            builder.ToTable("TaskRequiredSkills");

            builder.HasKey(trs => trs.Id);

            builder.Property(trs => trs.RequiredLevel)
                .IsRequired();

            builder.HasIndex(trs => new { trs.TaskId, trs.SkillId })
                .IsUnique();

            builder.HasIndex(trs => trs.SkillId);
        }
    }
}