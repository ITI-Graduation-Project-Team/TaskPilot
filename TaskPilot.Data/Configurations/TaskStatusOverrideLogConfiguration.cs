using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;

namespace TaskPilot.Data.Configurations
{
    public class TaskStatusOverrideLogConfiguration : IEntityTypeConfiguration<TaskStatusOverrideLog>
    {
        public void Configure(EntityTypeBuilder<TaskStatusOverrideLog> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.ReasonEn)
                .IsRequired()
                .HasMaxLength(1000);

            builder.Property(x => x.ReasonAr)
                .HasMaxLength(1000);

            builder.Property(x => x.OverrideType)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasOne(x => x.Task)
                .WithMany()
                .HasForeignKey(x => x.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
