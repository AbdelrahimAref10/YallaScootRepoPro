using Domain.Enums;

namespace Application.Features.Settlement.DTOs
{
    /// <summary>One order (or the old cash float) with money still open between the company and the party.</summary>
    public class SettlementOpenItemDto
    {
        public int? OrderId { get; set; }
        public string? OrderCode { get; set; }
        public DateTime Date { get; set; }
        public SettlementAllocationKind Kind { get; set; }
        /// <summary>Amount still open on this item (always positive).</summary>
        public decimal Open { get; set; }
    }

    /// <summary>What the company and a merchant / delivery owe each other right now.</summary>
    public class SettlementSummaryDto
    {
        public LedgerPartyType PartyType { get; set; }
        public int PartyId { get; set; }
        public string PartyName { get; set; } = string.Empty;
        /// <summary>Delivery: customer cash and old float he still holds (he owes it to the company).</summary>
        public decimal CashOwedToCompany { get; set; }
        /// <summary>Delivery commission or merchant earnings the company still owes the party.</summary>
        public decimal OwedByCompany { get; set; }
        /// <summary>OwedByCompany − CashOwedToCompany. Positive = company pays, negative = company collects.</summary>
        public decimal Net { get; set; }
        /// <summary>Direction of the next voucher; null when nothing is open.</summary>
        public SettlementDirection? Direction { get; set; }
        /// <summary>Largest cash amount a voucher can move (|Net|).</summary>
        public decimal MaxAmount { get; set; }
        /// <summary>Open items, oldest first (the order vouchers settle them in).</summary>
        public List<SettlementOpenItemDto> OpenItems { get; set; } = new();
    }

    public class SettlementVoucherDto
    {
        public int SettlementVoucherId { get; set; }
        public string VoucherNo { get; set; } = string.Empty;
        public LedgerPartyType PartyType { get; set; }
        public int PartyId { get; set; }
        public string PartyName { get; set; } = string.Empty;
        public SettlementDirection Direction { get; set; }
        public decimal Amount { get; set; }
        public string? Note { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class SettlementAllocationDto
    {
        public int? OrderId { get; set; }
        public string? OrderCode { get; set; }
        public SettlementAllocationKind Kind { get; set; }
        public decimal Amount { get; set; }
    }

    public class SettlementVoucherDetailDto : SettlementVoucherDto
    {
        public List<SettlementAllocationDto> Allocations { get; set; } = new();
    }
}
