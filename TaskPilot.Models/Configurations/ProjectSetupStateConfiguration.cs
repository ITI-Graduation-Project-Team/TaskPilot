using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Configurations.Common;
using TaskPilot.Models.Entities;

namespace TaskPilot.Models.Configurations
{
    public class ProjectSetupStateConfiguration : AuditableEntityConfiguration<ProjectSetupState, Guid>
    {
        public override void Configure(EntityTypeBuilder<ProjectSetupState> builder)
        {
            base.Configure(builder);
            builder.ToTable("ProjectSetupStates");
            builder.HasIndex(x => x.ProjectId).IsUnique();
            builder.Property(x => x.TechStackSuggestionJson).HasColumnType("nvarchar(max)");
            builder.Property(x => x.TechStackError).HasMaxLength(2000);
            builder.Property(x => x.WbsError).HasMaxLength(2000);
            builder.Property(x => x.SkillsError).HasMaxLength(2000);
            builder.Property(x => x.RowVersion).IsRowVersion();

            builder.HasOne(x => x.Project)
                .WithOne(x => x.SetupState)
                .HasForeignKey<ProjectSetupState>(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
