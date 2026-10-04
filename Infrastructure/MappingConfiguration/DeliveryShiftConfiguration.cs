using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.MappingConfiguration
{
    public class DeliveryShiftConfiguration : IEntityTypeConfiguration<DeliveryShift>
    {
        public void Configure(EntityTypeBuilder<DeliveryShift> builder)
        {
            builder.ToTable("VO_DeliveryShift");

            builder.HasKey(x => x.DeliveryShiftId);

            builder.Property(x => x.DeliveryShiftId).HasColumnName("DeliveryShiftId").ValueGeneratedOnAdd().IsRequired();
            builder.Property(x => x.DeliveryId).HasColumnName("DeliveryId").IsRequired();
            builder.Property(x => x.ShiftId).HasColumnName("ShiftId").IsRequired();

            builder.Property(x => x.CreatedBy).HasColumnName("CreatedBy").HasMaxLength(256);
            builder.Property(x => x.CreatedDate).HasColumnName("CreatedDate").IsRequired();
            builder.Property(x => x.LastModifiedBy).HasColumnName("LastModifiedBy").HasMaxLength(256);
            builder.Property(x => x.LastModifiedDate).HasColumnName("LastModifiedDate").IsRequired();

            builder.HasIndex(x => new { x.DeliveryId, x.ShiftId })
                .IsUnique()
                .HasDatabaseName("IX_VO_DeliveryShift_Delivery_Shift");

            builder.HasOne(x => x.Delivery)
                .WithMany(d => d.DeliveryShifts)
                .HasForeignKey(x => x.DeliveryId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            builder.HasOne(x => x.Shift)
                .WithMany(s => s.DeliveryShifts)
                .HasForeignKey(x => x.ShiftId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        }
    }
}
