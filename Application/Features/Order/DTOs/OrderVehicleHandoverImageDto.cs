using Domain.Enums;

namespace Application.Features.Order.DTOs
{
    /// <summary>One of the four rider photos taken at a vehicle handover.</summary>
    public class OrderVehicleHandoverImageDto
    {
        public int VehicleId { get; set; }
        public HandoverStep Step { get; set; }
        public HandoverImagePosition Position { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public int? DeliveryId { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
