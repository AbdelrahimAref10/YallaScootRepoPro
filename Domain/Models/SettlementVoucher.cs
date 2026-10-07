using Domain.Common;
using Domain.Enums;

namespace Domain.Models
{
    /// <summary>
    /// One cash movement between the company and a merchant or delivery, settled against the
    /// party's open orders (oldest first). Its journal lines point back to it.
    /// </summary>
    public class SettlementVoucher : IAuditable
    {
        public int SettlementVoucherId { get; private set; }
        /// <summary>RCV-000001 for receipts, PAY-000001 for payments; set after the first save.</summary>
        public string VoucherNo { get; private set; } = string.Empty;
        public LedgerPartyType PartyType { get; private set; }
        public int PartyId { get; private set; }
        public SettlementDirection Direction { get; private set; }
        /// <summary>Cash that actually changed hands (after netting).</summary>
        public decimal Amount { get; private set; }
        public string? Note { get; private set; }
        /// <summary>Client-generated id; a repeated submit returns the same voucher.</summary>
        public Guid RequestId { get; private set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }

        public ICollection<SettlementAllocation> Allocations { get; private set; } = new List<SettlementAllocation>();

        private SettlementVoucher() { }

        public static SettlementVoucher Create(
            LedgerPartyType partyType,
            int partyId,
            SettlementDirection direction,
            decimal amount,
            Guid requestId,
            string? note,
            string? createdBy)
        {
            if (partyType == LedgerPartyType.Company)
                throw new ArgumentException("Vouchers are for merchants and deliveries", nameof(partyType));
            if (partyId <= 0)
                throw new ArgumentException("Party ID must be greater than zero", nameof(partyId));
            // Zero is allowed: a delivery whose cash and commission cancel out is settled without cash.
            if (amount < 0)
                throw new ArgumentException("Amount cannot be negative", nameof(amount));
            if (requestId == Guid.Empty)
                throw new ArgumentException("Request ID is required", nameof(requestId));

            return new SettlementVoucher
            {
                PartyType = partyType,
                PartyId = partyId,
                Direction = direction,
                Amount = amount,
                RequestId = requestId,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };
        }

        /// <summary>Sets the voucher number from its id (called once the id is known).</summary>
        public void AssignNumber()
        {
            if (SettlementVoucherId <= 0)
                throw new InvalidOperationException("Voucher must be saved before it gets a number");
            var prefix = Direction == SettlementDirection.CollectFromParty ? "RCV" : "PAY";
            VoucherNo = $"{prefix}-{SettlementVoucherId:D6}";
        }

        public void AddAllocation(int? orderId, SettlementAllocationKind kind, decimal amount) =>
            Allocations.Add(SettlementAllocation.Create(orderId, kind, amount));
    }

    public class SettlementAllocation
    {
        public int SettlementAllocationId { get; private set; }
        public int SettlementVoucherId { get; private set; }
        public int? OrderId { get; private set; }
        public SettlementAllocationKind Kind { get; private set; }
        public decimal Amount { get; private set; }

        public SettlementVoucher SettlementVoucher { get; private set; } = null!;
        public Order? Order { get; private set; }

        private SettlementAllocation() { }

        internal static SettlementAllocation Create(int? orderId, SettlementAllocationKind kind, decimal amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be greater than zero", nameof(amount));
            return new SettlementAllocation { OrderId = orderId, Kind = kind, Amount = amount };
        }
    }
}
