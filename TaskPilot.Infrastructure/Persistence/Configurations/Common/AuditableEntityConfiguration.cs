using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Domain.Common;

namespace TaskPilot.Infrastructure.Persistence.Configurations.Common
{
    public abstract class AuditableEntityConfiguration<TEntity, TId>
    : IEntityTypeConfiguration<TEntity>
    where TEntity : AuditableEntity<TId>
    {
        public virtual void Configure(EntityTypeBuilder<TEntity> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.CreatedAt)
                   .IsRequired();

            builder.Property(e => e.CreatedBy)
                   .IsRequired(false);

            builder.Property(e => e.ModifiedAt)
                   .IsRequired(false);

            builder.Property(e => e.ModifiedBy)
                   .IsRequired(false);

            builder.Property(e => e.IsDeleted)
                   .IsRequired()
                   .HasDefaultValue(false);

            builder.HasIndex(e => e.IsDeleted);

            builder.HasQueryFilter(e => !e.IsDeleted);
        }
    }
}
