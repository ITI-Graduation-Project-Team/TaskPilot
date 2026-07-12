using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;
using TaskPilot.Models.Entities;

namespace TaskPilot.Models.Configurations
{
    public class CalenderEventConfiguration : IEntityTypeConfiguration<CalenderEvent>
    {
        public void Configure(EntityTypeBuilder<CalenderEvent> builder)
        {
            builder.ToTable("CalenderEvents");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Title)
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(x => x.Description)
                   .HasMaxLength(1000);

            //1 emp m calenderevent
            builder.HasOne(x => x.Employee)
                   .WithMany() 
                   .HasForeignKey(x => x.EmployeeId)
                   .OnDelete(DeleteBehavior.Cascade); 
           // 1 task item can be in multible calenders 
            builder.HasOne(x => x.RelatedTask)
                   .WithMany()
                   .HasForeignKey(x => x.RelatedTaskId)
                   .OnDelete(DeleteBehavior.Restrict); 
        }
    }
}
