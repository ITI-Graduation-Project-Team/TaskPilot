using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;

public class ProjectEmployeeConfiguration : IEntityTypeConfiguration<ProjectEmployee>
{
    public void Configure(EntityTypeBuilder<ProjectEmployee> builder)
    {
        builder.ToTable("ProjectEmployees");

        builder.HasKey(pe => new { pe.ProjectId, pe.EmployeeId });

        builder.HasOne(pe => pe.Project)
            .WithMany(p => p.ProjectEmployees)
            .HasForeignKey(pe => pe.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pe => pe.Employee)
            .WithMany(e => e.ProjectEmployees)
             .HasForeignKey(pe => pe.EmployeeId)
              .IsRequired(false)
              .OnDelete(DeleteBehavior.Restrict);

  
        builder.Property(pe => pe.Role)
             .IsRequired()
             .HasConversion<int>();

        builder.HasIndex(pe => pe.EmployeeId);
        builder.HasIndex(pe => pe.ProjectId);
        builder.HasIndex(pe => new { pe.ProjectId, pe.EmployeeId })
            .IsUnique();
    }
}