namespace Domain.Models
{
    /// <summary>Per-vehicle settlement amounts loaded from payment detail snapshots (passed into Order methods).</summary>
    public sealed class VehicleSettlementSnapshot
    {
        public int VehicleId { get; init; }
        public int MerchantId { get; init; }
        public int DeliveryId { get; init; }
        public decimal VehicleRental { get; init; }
        public decimal OrderServiceFees { get; init; }
        public decimal DeliveryFeeShare { get; init; }
        /// <summary>Full delivery fee of this vehicle (<c>OrderVehicle.DeliveryFee</c>).</summary>
        public decimal VehicleDeliveryFee { get; init; }
        /// <summary>Company's cut of <see cref="VehicleRental"/>, snapshotted at Confirmed.</summary>
        public decimal MerchantCompanyCommission { get; init; }
        public bool MerchantCashOnReceive { get; init; }
    }
}
