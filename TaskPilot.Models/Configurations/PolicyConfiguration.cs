using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Configurations.Common;

namespace TaskPilot.Models.Configurations
{
    public class PolicyConfiguration
     : AuditableEntityConfiguration<Policy, Guid>
    {
        public override void Configure(EntityTypeBuilder<Policy> builder)
        {
            base.Configure(builder);

            builder.ToTable("Policies");

            builder.Property(p => p.TitleEn)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(p => p.TitleAr)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(p => p.ContentEn)
                .IsRequired(false);

            builder.Property(p => p.ContentAr)
                .IsRequired(false);

            builder.Property(p => p.DocumentUrl)
                .IsRequired(false);

            builder.Property(p => p.VersionNumber)
                .HasDefaultValue(1);

            builder.HasOne(p => p.Project)
                .WithMany(pr => pr.Policies)
                .HasForeignKey(p => p.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.Company)
                .WithMany(c => c.Policies)
                .HasForeignKey(p => p.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(p => p.ProjectId);
            builder.HasIndex(p => p.CompanyId);

            builder.HasIndex(p => new { p.ProjectId, p.VersionNumber })
                .IsUnique()
                .HasFilter("[ProjectId] IS NOT NULL");

            builder.HasIndex(p => new { p.CompanyId, p.VersionNumber })
                .IsUnique()
                .HasFilter("[CompanyId] IS NOT NULL");
        }
    }
}
