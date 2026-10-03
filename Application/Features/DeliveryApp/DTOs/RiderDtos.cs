using Domain.Enums;

namespace Application.Features.DeliveryApp.DTOs
{
    public class RiderProfileDto
    {
        public int DeliveryId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? PersonalImage { get; set; }
        public int CityId { get; set; }
        public string CityName { get; set; } = string.Empty;
        public int ZoneId { get; set; }
        public string ZoneName { get; set; } = string.Empty;
    }

    public class RiderShiftDto
    {
        public int ShiftId { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>Local (Egypt) "HH:mm".</summary>
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;

        /// <summary>Bitmask over DayOfWeek: Sunday = 1 … Saturday = 64.</summary>
        public int DaysOfWeekMask { get; set; }
    }

    public class RiderShiftStatusDto
    {
        /// <summary>Online for dispatch: switched on and inside a running shift.</summary>
        public bool IsOnline { get; set; }

        /// <summary>The rider's own toggle, whatever the shift.</summary>
        public bool OnlineToggle { get; set; }
        public bool IsInShift { get; set; }

        /// <summary>The toggle only works while a shift is running.</summary>
        public bool CanGoOnline { get; set; }
        public RiderShiftDto? CurrentShift { get; set; }

        /// <summary>UTC.</summary>
        public DateTime? CurrentShiftEndsAt { get; set; }
        public RiderShiftDto? NextShift { get; set; }

        /// <summary>UTC.</summary>
        public DateTime? NextShiftStartsAt { get; set; }
        public List<RiderShiftDto> Shifts { get; set; } = new();
    }

    public class RiderOrderSummaryDto
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public OrderState OrderState { get; set; }
        public bool IsUrgent { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string HotelName { get; set; } = string.Empty;
        public string DestinationZoneName { get; set; } = string.Empty;
        public DateTime ReservationDateFrom { get; set; }
        public DateTime ReservationDateTo { get; set; }
        public int MyVehiclesCount { get; set; }

        /// <summary>ReceiveFromOwner | DeliverToCustomer | ReceiveFromCustomer | DeliverToOwner | None.</summary>
        public string NextAction { get; set; } = RiderActions.None;
        public decimal CollectFromCustomer { get; set; }
        public bool IsPaidOnline { get; set; }
        public decimal MyDeliveryFeeShare { get; set; }
    }

    public class RiderOrderDetailDto
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public OrderState OrderState { get; set; }
        public bool IsUrgent { get; set; }
        public string? Notes { get; set; }
        public RiderCustomerDto Customer { get; set; } = new();
        public DateTime ReservationDateFrom { get; set; }
        public DateTime ReservationDateTo { get; set; }
        public List<RiderVehicleDto> Vehicles { get; set; } = new();
        public RiderOrderMoneyDto Money { get; set; } = new();
    }

    public class RiderCustomerDto
    {
        public string Name { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string HotelName { get; set; } = string.Empty;
        public string HotelAddress { get; set; } = string.Empty;
        public string? HotelPhone { get; set; }
        public string DestinationZoneName { get; set; } = string.Empty;
    }

    public class RiderMerchantDto
    {
        public int MerchantId { get; set; }
        public string MerchantName { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string ZoneName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    public class RiderLegDto
    {
        public int DeliveryId { get; set; }
        public string DeliveryName { get; set; } = string.Empty;
        public bool IsMine { get; set; }
    }

    public class RiderLegsDto
    {
        public RiderLegDto? Delivery { get; set; }
        public RiderLegDto? Return { get; set; }
    }

    public class RiderStepDto
    {
        public bool Done { get; set; }
        public DateTime? At { get; set; }
        public List<string> Images { get; set; } = new();
        public string? ByDeliveryName { get; set; }
    }

    public class RiderStepsDto
    {
        public RiderStepDto ReceivedFromOwner { get; set; } = new();
        public RiderStepDto DeliveredToCustomer { get; set; } = new();
        public RiderStepDto ReceivedFromCustomer { get; set; } = new();
        public RiderStepDto DeliveredToOwner { get; set; } = new();
    }

    public class RiderStepMoneyDto
    {
        /// <summary>CollectFromCustomer | None. Riders never pay merchants.</summary>
        public string Type { get; set; } = "None";
        public decimal Amount { get; set; }
    }

    public class RiderVehicleDto
    {
        public int VehicleId { get; set; }
        public string VehicleCode { get; set; } = string.Empty;
        public string VehicleName { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public RiderMerchantDto Merchant { get; set; } = new();
        public RiderLegsDto Legs { get; set; } = new();
        public RiderStepsDto Steps { get; set; } = new();
        public string NextAction { get; set; } = RiderActions.None;
        public RiderStepMoneyDto NextStepMoney { get; set; } = new();
        public bool DeliveryFailed { get; set; }
        public string? DeliveryFailureReason { get; set; }
    }

    public class RiderCollectBreakdownDto
    {
        public decimal Rental { get; set; }
        public decimal DeliveryFees { get; set; }
        public decimal ServiceFees { get; set; }
        public decimal UrgentFees { get; set; }
        public decimal TieredDiscount { get; set; }
        public decimal PreviousDebt { get; set; }
    }

    public class RiderOrderMoneyDto
    {
        public int PaymentMethod { get; set; }
        public bool IsPaidOnline { get; set; }
        public string CurrencyCode { get; set; } = "EGP";

        /// <summary>Cash this rider collects (or collected) from the customer on this order; 0 otherwise.</summary>
        public decimal CollectFromCustomer { get; set; }
        public RiderCollectBreakdownDto CollectFromCustomerBreakdown { get; set; } = new();

        /// <summary>This rider's commission on this order, across the legs he holds.</summary>
        public decimal MyDeliveryFeeShare { get; set; }
    }

    public class RiderWalletDto
    {
        public decimal CashCollected { get; set; }
        public decimal CashRemitted { get; set; }

        /// <summary>Cash the rider still owes the company (collected − remitted).</summary>
        public decimal CashDebt { get; set; }

        /// <summary>Set by the admin; null = no limit. At or above it no new cash orders until he remits.</summary>
        public decimal? CashDebtLimit { get; set; }
        public bool IsOverCashDebtLimit { get; set; }
        public decimal CommissionEarned { get; set; }
        public decimal CommissionPaid { get; set; }

        /// <summary>Fault clawbacks / fault debits charged to the rider.</summary>
        public decimal Deductions { get; set; }

        /// <summary>Commission the company still owes the rider (earned − paid − deductions).</summary>
        public decimal CommissionDue { get; set; }

        /// <summary>All ledger lines: positive = company owes the rider, negative = rider owes the company.</summary>
        public decimal Balance { get; set; }
        public string CurrencyCode { get; set; } = "EGP";
    }

    public class RiderJournalDto
    {
        public int Id { get; set; }
        public int? OrderId { get; set; }
        public string? OrderCode { get; set; }
        public OrderJournalEntryKind EntryKind { get; set; }

        /// <summary>Positive = in the rider's favour (credit), negative = against (debit).</summary>
        public decimal Amount { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class RiderNotificationDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public int? OrderId { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public static class RiderActions
    {
        public const string None = "None";
        public const string ReceiveFromOwner = "ReceiveFromOwner";
        public const string DeliverToCustomer = "DeliverToCustomer";
        public const string ReceiveFromCustomer = "ReceiveFromCustomer";
        public const string DeliverToOwner = "DeliverToOwner";

        public static string For(HandoverStep? step) => step switch
        {
            HandoverStep.ReceivedFromOwner => ReceiveFromOwner,
            HandoverStep.DeliveredToCustomer => DeliverToCustomer,
            HandoverStep.ReceivedFromCustomer => ReceiveFromCustomer,
            HandoverStep.DeliveredToOwner => DeliverToOwner,
            _ => None
        };
    }

    public enum RiderOrderTab
    {
        Pickup = 1,
        Return = 2,
        Completed = 3
    }
}
