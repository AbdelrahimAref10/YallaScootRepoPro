namespace Application.Features.Order.DTOs
{
    /// <summary>
    /// What the customer pays, line by line:
    /// Total = SubTotal + ServiceFees + DeliveryFees + UrgentFees − Discount + PreviousDebt.
    /// </summary>
    public class OrderPriceBreakdownDto
    {
        /// <summary>Vehicle rental: each vehicle's daily price × days.</summary>
        public decimal SubTotal { get; set; }
        public decimal ServiceFees { get; set; }
        public decimal DeliveryFees { get; set; }
        /// <summary>0 unless the order is urgent.</summary>
        public decimal UrgentFees { get; set; }
        /// <summary>Tiered (days) discount, as a positive amount to subtract.</summary>
        public decimal Discount { get; set; }
        /// <summary>Cancellation debt from an earlier order, carried into this one.</summary>
        public decimal PreviousDebt { get; set; }
        public decimal Total { get; set; }
    }
}
