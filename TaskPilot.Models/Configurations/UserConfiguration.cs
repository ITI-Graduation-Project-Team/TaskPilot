using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;


namespace TaskPilot.Models.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.Property(u => u.CreatedAt)
                   .HasDefaultValueSql("GETUTCDATE()");

            builder.Property(u => u.CreatedBy)
                .IsRequired(false);

            builder.Property(u => u.ModifiedAt)
                .IsRequired(false);

            builder.Property(u => u.ModifiedBy)
                .IsRequired(false);

            builder.Property(u => u.IsDeleted)
                .HasDefaultValue(false);

            builder.HasIndex(u => u.IsDeleted);

            builder.HasQueryFilter(u => !u.IsDeleted);

            builder.HasDiscriminator<string>("UserType")
                .HasValue<Employee>("Employee")
                .HasValue<ProjectManager>("ProjectManager")
                .HasValue<Admin>("Admin");

            builder.Property<string>("UserType")
                 .HasMaxLength(50)
                 .IsRequired();

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

            builder.HasIndex(u => u.CompanyId);

            builder.Property(u => u.UserName)
                 .HasMaxLength(256);

            builder.Property(u => u.Email)
                .HasMaxLength(256);
        }
    }
}