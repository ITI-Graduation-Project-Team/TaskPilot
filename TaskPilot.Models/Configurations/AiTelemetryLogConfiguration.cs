using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;

namespace TaskPilot.Models.Configurations
{
    public class AiTelemetryLogConfiguration : IEntityTypeConfiguration<AiTelemetryLog>
    {
        public void Configure(EntityTypeBuilder<AiTelemetryLog> builder)
        {
            builder.HasKey(log => log.Id);

            builder.Property(log => log.OperationType)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(log => log.ModelName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(log => log.Status)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(log => log.EstimatedCostUsd)
                .HasColumnType("decimal(18,6)");

            builder.Property(log => log.Timestamp)
                .HasDefaultValueSql("GETUTCDATE()");

            // Relationships
            builder.HasOne(log => log.User)
                .WithMany()
                .HasForeignKey(log => log.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(log => log.Project)
                .WithMany()
                .HasForeignKey(log => log.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes
            builder.HasIndex(log => log.UserId);
            builder.HasIndex(log => log.ProjectId);
            builder.HasIndex(log => log.Timestamp);
        }
    }
}
