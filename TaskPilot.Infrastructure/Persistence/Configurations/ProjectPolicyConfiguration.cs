using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Domain.Entities;
using TaskPilot.Infrastructure.Persistence.Configurations.Common;

namespace TaskPilot.Infrastructure.Persistence.Configurations
{
    public class ProjectPolicyConfiguration : AuditableEntityConfiguration<ProjectPolicy, Guid>
    {
        public override void Configure(EntityTypeBuilder<ProjectPolicy> builder)
        {
            base.Configure(builder);

            builder.ToTable("ProjectPolicies");

            builder.Property(pp => pp.TitleEn)
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(pp => pp.TitleAr)
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(pp => pp.ContentEn)
                   .IsRequired();

            builder.Property(pp => pp.ContentAr)
                   .IsRequired();

            builder.HasOne(pp => pp.Project)
                   .WithMany(p => p.Policies)
                   .HasForeignKey(pp => pp.ProjectId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
