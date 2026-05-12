using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Configurations.Common;
using TaskPilot.Models.Entities;

namespace TaskPilot.Data.Configurations
{
    public class EmployeeInvitationConfiguration
        : AuditableEntityConfiguration<EmployeeInvitation, Guid>
    {
        public override void Configure(
            EntityTypeBuilder<EmployeeInvitation> builder)
        {
            base.Configure(builder);

            builder.ToTable("EmployeeInvitations");

            // Primary Key

            builder.HasKey(x => x.Id);

            // Email

            builder.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(256);

            // Token

            builder.Property(x => x.Token)
                .IsRequired()
                .HasMaxLength(500);

            // ExpiresAt

            builder.Property(x => x.ExpiresAt)
                .IsRequired();

            // IsAccepted

            builder.Property(x => x.IsAccepted)
                .HasDefaultValue(false);

            // Invited By Relationship

            builder.HasOne(x => x.InvitedBy)
                .WithMany()
                .HasForeignKey(x => x.InvitedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes

            builder.HasIndex(x => x.Email);

            builder.HasIndex(x => x.Token)
                .IsUnique();

            builder.HasIndex(x =>
                new
                {
                    x.CompanyId,
                    x.Email
                });

            // Optional:
            // Prevent duplicate active invitations

            builder.HasIndex(x =>
                new
                {
                    x.CompanyId,
                    x.Email,
                    x.IsAccepted
                });
        }
    }
}