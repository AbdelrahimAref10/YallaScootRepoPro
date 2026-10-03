using Domain.Common;
using Domain.Enums;

namespace Domain.Models
{
    /// <summary>
    /// Per-vehicle, per-leg rider commission snapshot built at Assign Delivery:
    /// <c>DeliveryFeeShare = OrderVehicle.DeliveryFee × CommissionPercent / 100</c>,
    /// with the percent taken from the city at assignment time.
    /// </summary>
    public class DeliveryOrderPaymentDetail : IAuditable
    {
        public int DeliveryOrderPaymentDetailId { get; private set; }
        public int OrderId { get; private set; }
        public int DeliveryId { get; private set; }
        public int VehicleId { get; private set; }
        public DeliveryLeg Leg { get; private set; } = DeliveryLeg.Delivery;
        public decimal CommissionPercent { get; private set; }
        public decimal DeliveryFeeShare { get; private set; }

        public Order Order { get; private set; } = null!;
        public Delivery Delivery { get; private set; } = null!;
        public Vehicle Vehicle { get; private set; } = null!;

        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }

        private DeliveryOrderPaymentDetail() { }

        public static DeliveryOrderPaymentDetail Create(
            int orderId,
            int deliveryId,
            int vehicleId,
            decimal deliveryFeeShare,
            string? createdBy = null,
            DeliveryLeg leg = DeliveryLeg.Delivery,
            decimal commissionPercent = 100m)
        {
            if (orderId <= 0)
                throw new ArgumentException("Order ID must be greater than zero", nameof(orderId));
            if (deliveryId <= 0)
                throw new ArgumentException("Delivery ID must be greater than zero", nameof(deliveryId));
            if (vehicleId <= 0)
                throw new ArgumentException("Vehicle ID must be greater than zero", nameof(vehicleId));
            if (deliveryFeeShare < 0)
                throw new ArgumentException("Delivery fee share cannot be negative", nameof(deliveryFeeShare));
            if (commissionPercent < 0 || commissionPercent > 100)
                throw new ArgumentException("Commission percent must be between 0 and 100", nameof(commissionPercent));

            return new DeliveryOrderPaymentDetail
            {
                OrderId = orderId,
                DeliveryId = deliveryId,
                VehicleId = vehicleId,
                Leg = leg,
                CommissionPercent = commissionPercent,
                DeliveryFeeShare = deliveryFeeShare,
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };
        }

        /// <summary>Commission for one leg of one vehicle, rounded to 2 decimals.</summary>
        public static decimal ComputeShare(decimal vehicleDeliveryFee, decimal commissionPercent) =>
            Math.Round(vehicleDeliveryFee * commissionPercent / 100m, 2, MidpointRounding.AwayFromZero);
    }
}
