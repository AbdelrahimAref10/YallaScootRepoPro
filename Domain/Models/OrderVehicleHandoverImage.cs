using Domain.Common;
using Domain.Enums;

namespace Domain.Models
{
    /// <summary>
    /// One of the four mandatory photos taken by the rider at a handover.
    /// The legacy single <c>OrderVehicle.*ImageUrl</c> keeps the Front photo.
    /// </summary>
    public class OrderVehicleHandoverImage : IAuditable
    {
        public int OrderVehicleHandoverImageId { get; private set; }
        public int OrderId { get; private set; }
        public int VehicleId { get; private set; }
        public HandoverStep Step { get; private set; }
        public HandoverImagePosition Position { get; private set; }
        public string ImageUrl { get; private set; } = string.Empty;
        public int? DeliveryId { get; private set; }

        public Order Order { get; private set; } = null!;

        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }

        private OrderVehicleHandoverImage() { }

        public static OrderVehicleHandoverImage Create(
            int orderId,
            int vehicleId,
            HandoverStep step,
            HandoverImagePosition position,
            string imageUrl,
            int? deliveryId,
            string? createdBy = null)
        {
            if (orderId <= 0)
                throw new ArgumentException("Order ID must be greater than zero", nameof(orderId));
            if (vehicleId <= 0)
                throw new ArgumentException("Vehicle ID must be greater than zero", nameof(vehicleId));
            if (string.IsNullOrWhiteSpace(imageUrl))
                throw new ArgumentException("Image URL cannot be empty", nameof(imageUrl));

            return new OrderVehicleHandoverImage
            {
                OrderId = orderId,
                VehicleId = vehicleId,
                Step = step,
                Position = position,
                ImageUrl = imageUrl,
                DeliveryId = deliveryId,
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };
        }
    }
}
