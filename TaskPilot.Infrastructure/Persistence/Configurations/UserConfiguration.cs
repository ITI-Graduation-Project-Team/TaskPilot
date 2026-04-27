using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Domain.Entities;
using TaskPilot.Infrastructure.Persistence.Configurations.Common;

namespace TaskPilot.Infrastructure.Persistence.Configurations
{
    public class UserConfiguration : AuditableEntityConfiguration<User, Guid>
    {
        public override void Configure(EntityTypeBuilder<User> builder)
        {
            base.Configure(builder);
            builder.HasDiscriminator<string>("Role")
           .HasValue<Developer>("Developer")
           .HasValue<ProjectManager>("ProjectManager");

        }

    }
}
