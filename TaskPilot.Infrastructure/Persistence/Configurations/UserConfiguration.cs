using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Domain.Entities;
using TaskPilot.Infrastructure.Identity;
using TaskPilot.Infrastructure.Persistence.Configurations.Common;

namespace TaskPilot.Infrastructure.Persistence.Configurations
{
    public class UserConfiguration : AuditableEntityConfiguration<User, Guid>
    {
        public override void Configure(EntityTypeBuilder<User> builder)
        {
            base.Configure(builder);

            builder.ToTable("Users");

           
            builder.HasDiscriminator<string>("UserType")
                .HasValue<Employee>("Employee")
                .HasValue<ProjectManager>("ProjectManager");

            builder.Property("UserType")
                .HasMaxLength(50);

           
            builder.HasOne<ApplicationUser>()
                .WithOne(au => au.User)
                .HasForeignKey<User>(u => u.ApplicationUserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(u => u.ApplicationUserId)
                .IsRequired();

            builder.HasIndex(u => u.ApplicationUserId)
                .IsUnique();

            
            builder.HasOne(u => u.Company)
                .WithMany(c => c.Users)
                .HasForeignKey(u => u.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(u => u.CompanyId);

           
            builder.Property(u => u.FirstNameEn)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(u => u.LastNameEn)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(u => u.FirstNameAr)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(u => u.LastNameAr)
                .IsRequired()
                .HasMaxLength(100);

            builder.HasIndex(u => new { u.CompanyId, u.ApplicationUserId });
        }
    }
}