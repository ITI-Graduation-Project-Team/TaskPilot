using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskPilot.Models.Common;

namespace TaskPilot.Models.Configurations.Common
{
    public abstract class AuditableEntityConfiguration<TEntity, TId>
    : IEntityTypeConfiguration<TEntity>
    where TEntity : AuditableEntity<TId>
    {
        public virtual void Configure(EntityTypeBuilder<TEntity> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.CreatedAt)
                   .HasDefaultValueSql("GETUTCDATE()");

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
