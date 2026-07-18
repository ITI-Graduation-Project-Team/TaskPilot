using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Entities;

namespace TaskPilot.Models.Configurations
{
    public class ProjectChatSessionConfiguration : IEntityTypeConfiguration<ProjectChatSession>
    {
        public void Configure(EntityTypeBuilder<ProjectChatSession> builder)
        {
            builder.HasKey(x => x.Id);

            builder.HasOne(x => x.Project)
                .WithOne()
                .HasForeignKey<ProjectChatSession>(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => x.ProjectId)
                .IsUnique();

            builder.Property(x => x.BrdExtractedText)
                .HasColumnType("nvarchar(max)");
        }
    }
}
