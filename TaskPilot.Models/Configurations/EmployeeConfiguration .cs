using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.Property(e => e.JobTitle)
                    .HasMaxLength(150);

        builder.Property(e => e.SeniorityLevel)
            .HasConversion<string>();

        builder.Property(e => e.HistoricalVelocity)
            .HasPrecision(10, 2);

        builder.Property(e => e.MaxSprintHours)
            .HasPrecision(10, 2);

        builder.Property(e => e.AvailabilityStatus)
            .HasConversion<string>();

        builder.HasIndex(e => e.AvailabilityStatus);
        builder.HasIndex(e => new { e.AvailabilityStatus, e.MaxSprintHours });
        builder.HasIndex(e => e.SeniorityLevel);
        builder.HasIndex(e => e.AvailabilityStatus);
        builder.HasIndex(e => e.JobTitle);
    }
}