using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.MappingConfiguration
{
    public class OrderVehicleHandoverImageConfiguration : IEntityTypeConfiguration<OrderVehicleHandoverImage>
    {
        public void Configure(EntityTypeBuilder<OrderVehicleHandoverImage> builder)
        {
            builder.ToTable("VO_OrderVehicleHandoverImage");

            builder.HasKey(x => x.OrderVehicleHandoverImageId);

            builder.Property(x => x.OrderVehicleHandoverImageId).HasColumnName("OrderVehicleHandoverImageId").ValueGeneratedOnAdd().IsRequired();
            builder.Property(x => x.OrderId).HasColumnName("OrderId").IsRequired();
            builder.Property(x => x.VehicleId).HasColumnName("VehicleId").IsRequired();
            builder.Property(x => x.Step).HasColumnName("Step").IsRequired();
            builder.Property(x => x.Position).HasColumnName("Position").IsRequired();
            builder.Property(x => x.ImageUrl).HasColumnName("ImageUrl").HasMaxLength(500).IsRequired();
            builder.Property(x => x.DeliveryId).HasColumnName("DeliveryId");

            builder.Property(x => x.CreatedBy).HasColumnName("CreatedBy").HasMaxLength(256);
            builder.Property(x => x.CreatedDate).HasColumnName("CreatedDate").IsRequired();
            builder.Property(x => x.LastModifiedBy).HasColumnName("LastModifiedBy").HasMaxLength(256);
            builder.Property(x => x.LastModifiedDate).HasColumnName("LastModifiedDate").IsRequired();

            builder.HasIndex(x => new { x.OrderId, x.VehicleId, x.Step, x.Position })
                .IsUnique()
                .HasDatabaseName("IX_VO_OrderVehicleHandoverImage_Order_Vehicle_Step_Position");

            builder.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        }
    }
}
