using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {

        builder.Property(e => e.HistoricalVelocity)
            .HasColumnType("decimal(10,2)");

        builder.Property(e => e.MaxSprintHours)
            .HasColumnType("decimal(10,2)");

        builder.Property(e => e.AvailabilityStatus)
            .HasConversion<int>();

        builder.HasIndex(e => e.AvailabilityStatus);
        builder.HasIndex(e => new { e.AvailabilityStatus, e.MaxSprintHours });
    }
}