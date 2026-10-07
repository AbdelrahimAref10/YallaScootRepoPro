using Application.Features.Settlement.DTOs;
using CSharpFunctionalExtensions;
using Domain.Enums;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Settlement
{
    /// <summary>
    /// Open money per order between the company and one merchant / delivery, read from the party's
    /// journal lines. Uses the same entry kinds as the rider wallet (GetRiderWalletQuery).
    /// </summary>
    public static class SettlementBalances
    {
        public static async Task<Result<SettlementSummaryDto>> GetAsync(
            DatabaseContext context, LedgerPartyType partyType, int partyId, CancellationToken cancellationToken)
        {
            var partyName = partyType switch
            {
                LedgerPartyType.Delivery => await context.Deliveries.Where(d => d.DeliveryId == partyId).Select(d => d.FullName).FirstOrDefaultAsync(cancellationToken),
                LedgerPartyType.Merchant => await context.Merchants.Where(m => m.MerchantId == partyId).Select(m => m.FullName).FirstOrDefaultAsync(cancellationToken),
                _ => null
            };
            if (partyName == null)
                return Result.Failure<SettlementSummaryDto>(partyType is LedgerPartyType.Delivery or LedgerPartyType.Merchant
                    ? "Party not found"
                    : "Settlements are only for merchants and deliveries");

            var sums = await context.OrderJournals
                .AsNoTracking()
                .Where(j => j.PartyType == partyType && j.PartyId == partyId)
                .GroupBy(j => new { j.OrderId, j.EntryKind, j.Direction })
                .Select(g => new { g.Key.OrderId, g.Key.EntryKind, g.Key.Direction, Amount = g.Sum(x => x.Amount) })
                .ToListAsync(cancellationToken);

            var orderIds = sums.Where(s => s.OrderId.HasValue).Select(s => s.OrderId!.Value).Distinct().ToList();
            var orders = await context.Orders
                .AsNoTracking()
                .Where(o => orderIds.Contains(o.OrderId))
                .Select(o => new { o.OrderId, o.OrderCode, o.CreatedDate })
                .ToDictionaryAsync(o => o.OrderId, cancellationToken);

            var items = new List<SettlementOpenItemDto>();
            foreach (var group in sums.GroupBy(s => s.OrderId))
            {
                decimal Sum(OrderJournalEntryKind kind, JournalDirection direction) =>
                    group.Where(s => s.EntryKind == kind && s.Direction == direction).Sum(s => s.Amount);

                var order = group.Key.HasValue && orders.TryGetValue(group.Key.Value, out var o) ? o : null;
                SettlementOpenItemDto Item(SettlementAllocationKind kind, decimal open) => new()
                {
                    OrderId = group.Key,
                    OrderCode = order?.OrderCode,
                    Date = order?.CreatedDate ?? DateTime.MinValue,
                    Kind = kind,
                    Open = Math.Round(open, 2)
                };

                if (partyType == LedgerPartyType.Merchant)
                {
                    var open = group.Where(s => s.Direction == JournalDirection.Credit).Sum(s => s.Amount)
                               - group.Where(s => s.Direction == JournalDirection.Debit).Sum(s => s.Amount);
                    if (group.Key.HasValue && open > 0)
                        items.Add(Item(SettlementAllocationKind.MerchantEarnings, open));
                    continue;
                }

                if (!group.Key.HasValue)
                {
                    // Old cash float (and cash a rider once advanced to a merchant) has no order.
                    var floatOpen = Sum(OrderJournalEntryKind.DeliveryCashFloatReceived, JournalDirection.Debit)
                                    - Sum(OrderJournalEntryKind.DeliveryCashFloatReturned, JournalDirection.Credit)
                                    - Sum(OrderJournalEntryKind.DeliveryCashAdvanceToMerchant, JournalDirection.Credit);
                    if (floatOpen > 0)
                        items.Add(Item(SettlementAllocationKind.DeliveryLegacyFloat, floatOpen));
                    continue;
                }

                var cashOpen = Sum(OrderJournalEntryKind.CashCollectedFromCustomer, JournalDirection.Debit)
                               - Sum(OrderJournalEntryKind.DeliveryRemittanceToCompany, JournalDirection.Credit)
                               - Sum(OrderJournalEntryKind.DeliveryCashAdvanceToMerchant, JournalDirection.Credit);
                if (cashOpen > 0)
                    items.Add(Item(SettlementAllocationKind.DeliveryCash, cashOpen));

                var commissionOpen = Sum(OrderJournalEntryKind.DeliveryFeeAccrued, JournalDirection.Credit)
                                     - Sum(OrderJournalEntryKind.DeliveryPaidByCompany, JournalDirection.Debit)
                                     - Sum(OrderJournalEntryKind.FaultClawback, JournalDirection.Debit)
                                     - Sum(OrderJournalEntryKind.NonDeliveryFaultDebit, JournalDirection.Debit);
                if (commissionOpen > 0)
                    items.Add(Item(SettlementAllocationKind.DeliveryCommission, commissionOpen));
            }

            // Oldest first; the float (no order) is settled before any order.
            items = items
                .OrderBy(i => i.OrderId.HasValue)
                .ThenBy(i => i.Date)
                .ThenBy(i => i.OrderId)
                .ToList();

            var cashOwed = items.Where(i => i.Kind is SettlementAllocationKind.DeliveryCash or SettlementAllocationKind.DeliveryLegacyFloat).Sum(i => i.Open);
            var owedByCompany = items.Where(i => i.Kind is SettlementAllocationKind.DeliveryCommission or SettlementAllocationKind.MerchantEarnings).Sum(i => i.Open);
            var net = owedByCompany - cashOwed;

            return Result.Success(new SettlementSummaryDto
            {
                PartyType = partyType,
                PartyId = partyId,
                PartyName = partyName,
                CashOwedToCompany = cashOwed,
                OwedByCompany = owedByCompany,
                Net = net,
                Direction = items.Count == 0 ? null : net < 0 ? SettlementDirection.CollectFromParty : SettlementDirection.PayToParty,
                MaxAmount = Math.Abs(net),
                OpenItems = items
            });
        }
    }
}
