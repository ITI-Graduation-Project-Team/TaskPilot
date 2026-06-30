using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Configurations.Common;

namespace TaskPilot.Models.Configurations
{
    public class ProjectConfiguration : AuditableEntityConfiguration<Project, Guid>
    {
        public override void Configure(EntityTypeBuilder<Project> builder)
        {
            base.Configure(builder);

            builder.ToTable("Projects");

            builder.Property(p => p.NameEn)
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(p => p.NameAr)
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(p => p.DescriptionEn)
                   .HasMaxLength(1000);

            builder.Property(p => p.DescriptionAr)
                   .HasMaxLength(1000);

            builder.HasOne(p => p.Manager)
                   .WithMany()
                   .HasForeignKey(p => p.ManagerId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(p => p.Policies)
                   .WithOne(pp => pp.Project)
                   .HasForeignKey(pp => pp.ProjectId)
                   .OnDelete(DeleteBehavior.Cascade);


            builder.HasMany(p => p.ProjectEmployees)
                  .WithOne(pe => pe.Project)
                   .HasForeignKey(pe => pe.ProjectId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(p => p.Manager)
                .WithMany(pm => pm.ManagedProjects)
                 .HasForeignKey(p => p.ManagerId)
              .OnDelete(DeleteBehavior.Restrict);


            builder.HasIndex(p => p.CompanyId);
            builder.HasIndex(p => p.ManagerId);
            builder.HasIndex(p => new { p.CompanyId, p.NameEn })
                .IsUnique();
            builder.HasIndex(p => new { p.CompanyId, p.ManagerId });
        }
    }
}
