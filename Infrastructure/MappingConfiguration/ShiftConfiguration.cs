using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.MappingConfiguration
{
    public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
    {
        public void Configure(EntityTypeBuilder<Shift> builder)
        {
            builder.ToTable("VO_Shift");

            builder.HasKey(x => x.ShiftId);

            builder.Property(x => x.ShiftId).HasColumnName("ShiftId").ValueGeneratedOnAdd().IsRequired();
            builder.Property(x => x.CityId).HasColumnName("CityId").IsRequired();
            builder.Property(x => x.Name).HasColumnName("Name").HasMaxLength(200).IsRequired();
            builder.Property(x => x.StartTime).HasColumnName("StartTime").HasColumnType("time").IsRequired();
            builder.Property(x => x.EndTime).HasColumnName("EndTime").HasColumnType("time").IsRequired();
            builder.Property(x => x.DaysOfWeekMask).HasColumnName("DaysOfWeekMask").HasDefaultValue(Shift.AllDays).IsRequired();
            builder.Property(x => x.IsActive).HasColumnName("IsActive").IsRequired();
            builder.Property(x => x.IsDeleted).HasColumnName("IsDeleted").HasDefaultValue(false).IsRequired();

            builder.Property(x => x.CreatedBy).HasColumnName("CreatedBy").HasMaxLength(256);
            builder.Property(x => x.CreatedDate).HasColumnName("CreatedDate").IsRequired();
            builder.Property(x => x.LastModifiedBy).HasColumnName("LastModifiedBy").HasMaxLength(256);
            builder.Property(x => x.LastModifiedDate).HasColumnName("LastModifiedDate").IsRequired();

            builder.HasIndex(x => x.CityId).HasDatabaseName("IX_VO_Shift_CityId");

            builder.HasOne(x => x.City)
                .WithMany()
                .HasForeignKey(x => x.CityId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
        }
    }
}
