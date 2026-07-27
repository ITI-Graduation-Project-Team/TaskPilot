using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;

namespace TaskPilot.Models.Configurations
{
    public class SprintRetrospectiveConfiguration : IEntityTypeConfiguration<SprintRetrospective>
    {
        public void Configure(EntityTypeBuilder<SprintRetrospective> builder)
        {
            builder.HasKey(sr => sr.Id);

            builder.HasOne(sr => sr.Sprint)
                .WithOne()
                .HasForeignKey<SprintRetrospective>(sr => sr.SprintId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
