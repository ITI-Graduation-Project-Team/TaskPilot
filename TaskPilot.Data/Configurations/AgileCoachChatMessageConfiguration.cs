using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.AgileCoach;

namespace TaskPilot.Data.Configurations
{
    public class AgileCoachChatMessageConfiguration : IEntityTypeConfiguration<AgileCoachChatMessage>
    {
        public void Configure(EntityTypeBuilder<AgileCoachChatMessage> builder)
        {
            builder.ToTable("AgileCoachChatMessages");

            builder.HasKey(x => x.Id);

            builder.HasOne(x => x.Task)
                .WithMany()
                .HasForeignKey(x => x.TaskId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            builder.Property(x => x.Role)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(x => x.Content)
                .IsRequired();

            builder.Property(x => x.Lang)
                .IsRequired()
                .HasMaxLength(5);

            builder.HasQueryFilter(x => !x.IsDeleted);
        }
    }
}
