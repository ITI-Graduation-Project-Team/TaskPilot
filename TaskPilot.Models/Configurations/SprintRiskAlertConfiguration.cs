using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;

namespace TaskPilot.Models.Configurations
{
    public class SprintRiskAlertConfiguration : IEntityTypeConfiguration<SprintRiskAlert>
    {
        public void Configure(EntityTypeBuilder<SprintRiskAlert> builder)
        {
            builder.Property(a => a.RiskType)
                .HasConversion<string>();

            builder.Property(a => a.Severity)
                .HasConversion<string>();

            builder.HasIndex(a => a.SprintId);
            builder.HasIndex(a => a.LastDetectedAt);
            builder.HasIndex(a => a.IsDismissed);
        }
    }
}
