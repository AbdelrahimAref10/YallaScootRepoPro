using Domain.Enums;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Delivery.Common
{
    /// <summary>
    /// Cash a rider collected from customers and still owes the company, from the order ledger.
    /// Lines from before riders stopped handling merchant cash / floats still count.
    /// </summary>
    public static class RiderCashDebt
    {
        public static async Task<Dictionary<int, decimal>> ForRidersAsync(
            DatabaseContext context,
            IReadOnlyCollection<int> deliveryIds,
            CancellationToken cancellationToken)
        {
            if (deliveryIds.Count == 0)
                return new Dictionary<int, decimal>();

            var kinds = new[]
            {
                OrderJournalEntryKind.CashCollectedFromCustomer,
                OrderJournalEntryKind.DeliveryRemittanceToCompany,
                OrderJournalEntryKind.DeliveryCashFloatReceived,
                OrderJournalEntryKind.DeliveryCashFloatReturned,
                OrderJournalEntryKind.DeliveryCashAdvanceToMerchant
            };

            var sums = await context.OrderJournals
                .AsNoTracking()
                .Where(j => j.PartyType == LedgerPartyType.Delivery
                    && j.PartyId != null
                    && deliveryIds.Contains(j.PartyId.Value)
                    && kinds.Contains(j.EntryKind))
                .GroupBy(j => new { PartyId = j.PartyId!.Value, j.EntryKind, j.Direction })
                .Select(g => new { g.Key.PartyId, g.Key.EntryKind, g.Key.Direction, Amount = g.Sum(x => x.Amount) })
                .ToListAsync(cancellationToken);

            return deliveryIds.Distinct().ToDictionary(id => id, id =>
            {
                decimal Sum(OrderJournalEntryKind kind, JournalDirection direction) =>
                    sums.Where(s => s.PartyId == id && s.EntryKind == kind && s.Direction == direction).Sum(s => s.Amount);

                return Sum(OrderJournalEntryKind.CashCollectedFromCustomer, JournalDirection.Debit)
                    - Sum(OrderJournalEntryKind.DeliveryRemittanceToCompany, JournalDirection.Credit)
                    + Sum(OrderJournalEntryKind.DeliveryCashFloatReceived, JournalDirection.Debit)
                    - Sum(OrderJournalEntryKind.DeliveryCashFloatReturned, JournalDirection.Credit)
                    - Sum(OrderJournalEntryKind.DeliveryCashAdvanceToMerchant, JournalDirection.Credit);
            });
        }

        /// <summary>True when the rider has a limit and his debt reached it.</summary>
        public static bool IsOverLimit(decimal debt, decimal? limit) => limit.HasValue && debt >= limit.Value;
    }
}
