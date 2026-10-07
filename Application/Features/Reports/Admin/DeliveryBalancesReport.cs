using Domain.Enums;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using K = Domain.Enums.OrderJournalEntryKind;
using D = Domain.Enums.JournalDirection;

namespace Application.Features.Reports.Admin
{
    /// <summary>
    /// Per delivery: cash collected / handed in and commission earned / paid in the period, and at the end
    /// of it the cash he still holds, the commission still owed to him and the net (same rules as his wallet).
    /// </summary>
    public class DeliveryBalancesReport : IReport
    {
        private readonly DatabaseContext _context;
        public DeliveryBalancesReport(DatabaseContext context) => _context = context;

        public string Key => "delivery-balances";
        public ReportScope Scope => ReportScope.Admin;

        public async Task<ReportResultDto> RunAsync(ReportFilter f, ReportContext context, CancellationToken ct)
        {
            var deliveries = _context.Deliveries.AsNoTracking().Where(d => !d.IsDeleted);
            if (ReportFilter.Has(f.CityIds)) deliveries = deliveries.Where(d => f.CityIds!.Contains(d.CityId));
            if (ReportFilter.Has(f.DeliveryIds)) deliveries = deliveries.Where(d => f.DeliveryIds!.Contains(d.DeliveryId));
            var list = await deliveries
                .OrderBy(d => d.FullName)
                .Select(d => new { d.DeliveryId, d.FullName, d.MobileNumber, City = d.City.Name })
                .ToListAsync(ct);

            var sums = (await ReportJournal.SumsAsync(_context, LedgerPartyType.Delivery, list.Select(d => d.DeliveryId).ToList(), f, true, ct))
                .ToLookup(s => s.PartyId);

            var b = new ReportBuilder(Key, "Delivery balances")
                .Text("delivery", "Delivery").Text("mobile", "Mobile").Text("city", "City")
                .Number("orders", "Orders")
                .Money("cashCollected", "Cash collected").Money("cashHandedIn", "Cash handed in")
                .Money("commissionEarned", "Commission earned").Money("commissionPaid", "Commission paid").Money("deductions", "Deductions")
                .Money("cashHeld", "Cash held").Money("commissionDue", "Commission owed to him")
                .Money("net", "Net balance")
                .Money("owedToDelivery", "Owed to delivery").Money("owedByDelivery", "Owed by delivery");

            foreach (var d in list)
            {
                var s = sums[d.DeliveryId].ToList();
                if (s.Count == 0) continue;

                decimal Period(K kind, D dir) => ReportJournal.Total(s, kind, dir);
                decimal All(K kind, D dir) => ReportJournal.Total(s, kind, dir, beforePeriod: null);

                var cashHeld = All(K.CashCollectedFromCustomer, D.Debit) - All(K.DeliveryRemittanceToCompany, D.Credit)
                               + All(K.DeliveryCashFloatReceived, D.Debit) - All(K.DeliveryCashFloatReturned, D.Credit)
                               - All(K.DeliveryCashAdvanceToMerchant, D.Credit);
                var commissionDue = All(K.DeliveryFeeAccrued, D.Credit) - All(K.DeliveryPaidByCompany, D.Debit)
                                    - All(K.FaultClawback, D.Debit) - All(K.NonDeliveryFaultDebit, D.Debit);
                var net = commissionDue - cashHeld;

                b.Row(new()
                {
                    ["delivery"] = d.FullName,
                    ["mobile"] = d.MobileNumber,
                    ["city"] = d.City,
                    ["orders"] = s.Where(x => !x.BeforePeriod && x.OrderId.HasValue
                                              && x.Kind is K.DeliveryFeeAccrued or K.CashCollectedFromCustomer)
                                  .Select(x => x.OrderId).Distinct().Count(),
                    ["cashCollected"] = Period(K.CashCollectedFromCustomer, D.Debit),
                    ["cashHandedIn"] = Period(K.DeliveryRemittanceToCompany, D.Credit) + Period(K.DeliveryCashFloatReturned, D.Credit),
                    ["commissionEarned"] = Period(K.DeliveryFeeAccrued, D.Credit),
                    ["commissionPaid"] = Period(K.DeliveryPaidByCompany, D.Debit),
                    ["deductions"] = Period(K.FaultClawback, D.Debit) + Period(K.NonDeliveryFaultDebit, D.Debit),
                    ["cashHeld"] = cashHeld,
                    ["commissionDue"] = commissionDue,
                    ["net"] = net,
                    ["owedToDelivery"] = Math.Max(net, 0),
                    ["owedByDelivery"] = Math.Max(-net, 0)
                });
            }

            return b.Kpi("cashHeld", "Cash held by deliveries", b.Sum("cashHeld"))
                .Kpi("commissionDue", "Commission owed", b.Sum("commissionDue"))
                .Kpi("owedByDeliveries", "Net owed by deliveries", b.Sum("owedByDelivery"))
                .Kpi("owedToDeliveries", "Net owed to deliveries", b.Sum("owedToDelivery"))
                .Build();
        }
    }
}
