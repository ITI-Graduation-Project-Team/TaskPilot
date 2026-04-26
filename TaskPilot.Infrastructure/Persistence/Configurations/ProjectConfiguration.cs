using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Domain.Entities;
using TaskPilot.Infrastructure.Persistence.Configurations.Common;

namespace TaskPilot.Infrastructure.Persistence.Configurations
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

            builder.HasMany(p => p.Sprints)
                   .WithOne(s => s.Project)
                   .HasForeignKey(s => s.ProjectId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(p => p.Policies)
                   .WithOne(pp => pp.Project)
                   .HasForeignKey(pp => pp.ProjectId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
