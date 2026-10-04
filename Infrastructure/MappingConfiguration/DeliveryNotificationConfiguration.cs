using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.MappingConfiguration
{
    public class DeliveryNotificationConfiguration : IEntityTypeConfiguration<DeliveryNotification>
    {
        public void Configure(EntityTypeBuilder<DeliveryNotification> builder)
        {
            builder.ToTable("VO_DeliveryNotification");

            builder.HasKey(n => n.DeliveryNotificationId);

            builder.Property(n => n.DeliveryNotificationId).ValueGeneratedOnAdd();
            builder.Property(n => n.Title).HasMaxLength(500).IsRequired();
            builder.Property(n => n.Message).HasMaxLength(2000).IsRequired();
            builder.Property(n => n.NotificationType).IsRequired();
            builder.Property(n => n.IsRead).IsRequired().HasDefaultValue(false);
            builder.Property(n => n.CreatedBy).HasMaxLength(256);
            builder.Property(n => n.LastModifiedBy).HasMaxLength(256);
            builder.Property(n => n.CreatedDate).IsRequired();
            builder.Property(n => n.LastModifiedDate).IsRequired();

            builder.HasOne(n => n.Delivery)
                .WithMany()
                .HasForeignKey(n => n.DeliveryId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(n => n.Order)
                .WithMany()
                .HasForeignKey(n => n.OrderId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(n => n.DeliveryId);
            builder.HasIndex(n => n.IsRead);
            builder.HasIndex(n => n.CreatedDate);
            builder.HasIndex(n => n.OrderId);
        }
    }
}
