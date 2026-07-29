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

            builder.Property(sr => sr.AnalysisJson);
            builder.Property(sr => sr.ImprovementsJson);
            builder.Property(sr => sr.WhatWentWellEn).HasMaxLength(4000);
            builder.Property(sr => sr.WhatWentWellAr).HasMaxLength(4000);
            builder.Property(sr => sr.ChallengesEn).HasMaxLength(4000);
            builder.Property(sr => sr.ChallengesAr).HasMaxLength(4000);
            builder.Property(sr => sr.ActionItemsEn).HasMaxLength(4000);
            builder.Property(sr => sr.ActionItemsAr).HasMaxLength(4000);
            builder.Property(sr => sr.TeamSentimentSummaryEn).HasMaxLength(1000);
            builder.Property(sr => sr.TeamSentimentSummaryAr).HasMaxLength(1000);

            builder.HasOne(sr => sr.Sprint)
                .WithOne()
                .HasForeignKey<SprintRetrospective>(sr => sr.SprintId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
