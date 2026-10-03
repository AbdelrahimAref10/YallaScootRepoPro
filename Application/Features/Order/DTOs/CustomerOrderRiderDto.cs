using Domain.Enums;

namespace Application.Features.Order.DTOs
{
    /// <summary>A rider on one trip (delivery or return) of one vehicle, as the customer sees it.</summary>
    public class CustomerOrderRiderDto
    {
        public int VehicleId { get; set; }
        public string VehicleName { get; set; } = string.Empty;
        public string VehicleCode { get; set; } = string.Empty;
        /// <summary>1 = delivery trip, 2 = return trip.</summary>
        public DeliveryLeg Leg { get; set; }
        public string RiderName { get; set; } = string.Empty;
        /// <summary>True while this trip is under way; then <see cref="RiderMobileNumber"/> is set.</summary>
        public bool CanCall { get; set; }
        public string? RiderMobileNumber { get; set; }
    }
}
