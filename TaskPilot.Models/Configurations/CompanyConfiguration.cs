using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Configurations.Common;

namespace TaskPilot.Models.Configurations
{
    public class CompanyConfiguration
        : AuditableEntityConfiguration<Company, Guid>
    {
        public override void Configure(EntityTypeBuilder<Company> builder)
        {
            base.Configure(builder);

            builder.ToTable("Companies");

            builder.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.HasOne(c => c.Owner)
                .WithMany()
                .HasForeignKey(c => c.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(c => c.Users)
                .WithOne(u => u.Company)
                .HasForeignKey(u => u.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(c => c.Projects)
                .WithOne(p => p.Company)
                .HasForeignKey(p => p.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.Invitations)
               .WithOne(x => x.Company)
               .HasForeignKey(x => x.CompanyId)
               .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(c => c.Name);
            builder.HasIndex(c => c.OwnerId);
        }
    }
}