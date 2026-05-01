using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Configurations.Common;

namespace TaskPilot.Models.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {

            builder.ToTable("Users");
           
            builder.HasDiscriminator<string>("UserType")
                .HasValue<Employee>("Employee")
                .HasValue<ProjectManager>("ProjectManager");

            builder.Property("UserType")
                .HasMaxLength(50);


            
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

            builder.HasIndex(u => new { u.CompanyId, u.Id });
        }
    }
}