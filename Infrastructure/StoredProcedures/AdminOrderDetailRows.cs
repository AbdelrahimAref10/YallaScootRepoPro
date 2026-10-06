namespace Infrastructure.StoredProcedures
{
    /// <summary>
    /// Raw result sets of <c>dbo.usp_GetAdminOrderDetail</c>, in the order the procedure returns them.
    /// The Application layer maps these into <c>OrderDetailDto</c>.
    /// </summary>
    public sealed class AdminOrderDetailResult
    {
        public AdminOrderHeaderRow? Header { get; set; }
        public List<AdminOrderVehicleRow> Vehicles { get; } = new();
        public List<AdminOrderPaymentRow> Payments { get; } = new();
        public AdminOrderRefundRow? Refund { get; set; }
        public AdminOrderTotalsRow? Totals { get; set; }
        public AdminOrderCancellationFeeRow? CancellationFee { get; set; }
        public List<AdminMerchantOrderRow> MerchantOrders { get; } = new();
        public List<AdminMerchantPaymentRow> MerchantPayments { get; } = new();
        public List<AdminDeliveryLegRow> DeliveryLegs { get; } = new();
        public List<AdminDeliveryPaymentRow> DeliveryPayments { get; } = new();
        public List<AdminJournalRow> Journals { get; } = new();
        public List<AdminHandoverImageRow> HandoverImages { get; } = new();
    }

    public sealed class AdminOrderHeaderRow
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerMobileNumber { get; set; }
        public int SubCategoryId { get; set; }
        public string? SubCategoryName { get; set; }
        public int CityId { get; set; }
        public string? CityName { get; set; }
        public int DestinationZoneId { get; set; }
        public string? DestinationZoneName { get; set; }
        public DateTime ReservationDateFrom { get; set; }
        public DateTime ReservationDateTo { get; set; }
        public int VehiclesCount { get; set; }
        public decimal OrderSubTotal { get; set; }
        public decimal OrderTotal { get; set; }
        public decimal PreviousDebt { get; set; }
        public bool MoneyRefunded { get; set; }
        public string? Notes { get; set; }
        public string? PassportImage { get; set; }
        public string? HotelName { get; set; }
        public string? HotelAddress { get; set; }
        public string? HotelPhone { get; set; }
        public bool IsUrgent { get; set; }
        public int PaymentMethodId { get; set; }
        public int OrderState { get; set; }
        public DateTime CreatedDate { get; set; }
        public int? ReceiptFaultParty { get; set; }
        public string? ReceiptRejectNote { get; set; }
        public bool OrderTotalDebitedToCompany { get; set; }
        public bool OrderDeliveryFailed { get; set; }
        public string? OrderDeliveryFailureReason { get; set; }
        public int? OrderDeliveryFailureFaultParty { get; set; }
    }

    public sealed class AdminOrderVehicleRow
    {
        public int VehicleId { get; set; }
        public string? VehicleName { get; set; }
        public string? VehicleCode { get; set; }
        public string? ImageUrl { get; set; }
        public int MerchantId { get; set; }
        public string? MerchantName { get; set; }
        public bool? MerchantCashOnReceive { get; set; }
        public int? MerchantZoneId { get; set; }
        public string? MerchantZoneName { get; set; }
        public string? Status { get; set; }
        public string? Color { get; set; }
        public string? Type { get; set; }
        public string? Model { get; set; }
        public decimal Price { get; set; }
        public int? SpeedKmh { get; set; }
        public int? EngineCapacityCc { get; set; }
        public decimal DeliveryFee { get; set; }
        public bool ReceivedFromOwner { get; set; }
        public DateTime? ReceivedFromOwnerAt { get; set; }
        public string? ReceivedFromOwnerImageUrl { get; set; }
        public bool DeliveredToCustomer { get; set; }
        public DateTime? DeliveredToCustomerAt { get; set; }
        public string? DeliveredToCustomerImageUrl { get; set; }
        public bool ReceivedFromCustomer { get; set; }
        public DateTime? ReceivedFromCustomerAt { get; set; }
        public string? ReceivedFromCustomerImageUrl { get; set; }
        public bool DeliveredToOwner { get; set; }
        public DateTime? DeliveredToOwnerAt { get; set; }
        public string? DeliveredToOwnerImageUrl { get; set; }
        public bool DeliveryFailed { get; set; }
        public string? DeliveryFailureReason { get; set; }
        public int? DeliveryFailureFaultParty { get; set; }
        public int MerchantResponseStatus { get; set; }
    }

    public sealed class AdminOrderPaymentRow
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int PaymentMethodId { get; set; }
        public decimal Total { get; set; }
        public int State { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public sealed class AdminOrderRefundRow
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public int OrderId { get; set; }
        public decimal OrderTotal { get; set; }
        public decimal CancellationFees { get; set; }
        public decimal RefundableAmount { get; set; }
        public int State { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public sealed class AdminOrderTotalsRow
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public decimal SubTotal { get; set; }
        public decimal ServiceFees { get; set; }
        public decimal DeliveryFees { get; set; }
        public decimal UrgentFees { get; set; }
        public decimal TieredDiscount { get; set; }
        public decimal TotalAfterAllFees { get; set; }
    }

    public sealed class AdminOrderCancellationFeeRow
    {
        public int Id { get; set; }
        public decimal Withdraw { get; set; }
        public int State { get; set; }
    }

    public sealed class AdminMerchantOrderRow
    {
        public int MerchantOrderId { get; set; }
        public int OrderId { get; set; }
        public int MerchantId { get; set; }
        public string? MerchantName { get; set; }
        public int ResponseStatus { get; set; }
        public string? RejectReason { get; set; }
        public DateTime? RespondedAt { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public sealed class AdminMerchantPaymentRow
    {
        public int MerchantOrderPaymentDetailId { get; set; }
        public int OrderId { get; set; }
        public int MerchantId { get; set; }
        public string? MerchantName { get; set; }
        public int VehicleId { get; set; }
        public string? VehicleCode { get; set; }
        public decimal VehicleRental { get; set; }
        public decimal NetAmount { get; set; }
        public decimal CompanyCommissionPercent { get; set; }
        public decimal CompanyCommissionAmount { get; set; }
    }

    public sealed class AdminDeliveryLegRow
    {
        public int DeliveryMenOrderId { get; set; }
        public int OrderId { get; set; }
        public int VehicleId { get; set; }
        public string? VehicleCode { get; set; }
        public int DeliveryId { get; set; }
        public string? DeliveryName { get; set; }
        public bool DeliveryReceivedFromMerchant { get; set; }
        public DateTime? ReceivedFromMerchantAt { get; set; }
        public int Leg { get; set; }
    }

    public sealed class AdminDeliveryPaymentRow
    {
        public int DeliveryOrderPaymentDetailId { get; set; }
        public int OrderId { get; set; }
        public int DeliveryId { get; set; }
        public string? DeliveryName { get; set; }
        public int VehicleId { get; set; }
        public string? VehicleCode { get; set; }
        public decimal DeliveryFeeShare { get; set; }
        public int Leg { get; set; }
        public decimal CommissionPercent { get; set; }
    }

    public sealed class AdminJournalRow
    {
        public int OrderJournalId { get; set; }
        public int? OrderId { get; set; }
        public int? VehicleId { get; set; }
        public string? VehicleCode { get; set; }
        public int PartyType { get; set; }
        public int? PartyId { get; set; }
        public int Direction { get; set; }
        public decimal Amount { get; set; }
        public int EntryKind { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty;
        public int? FaultParty { get; set; }
        public string? Note { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public sealed class AdminHandoverImageRow
    {
        public int VehicleId { get; set; }
        public int Step { get; set; }
        public int Position { get; set; }
        public string? ImageUrl { get; set; }
        public int? DeliveryId { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
