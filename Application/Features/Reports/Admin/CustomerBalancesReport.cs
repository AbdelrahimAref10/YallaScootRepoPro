using Domain.Enums;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reports.Admin
{
    /// <summary>
    /// Per customer: orders and payments in the period, cancellation fees he still owes and PayPal refunds
    /// the company still owes him (as of the end of the period).
    /// </summary>
    public class CustomerBalancesReport : IReport
    {
        private readonly DatabaseContext _context;
        public CustomerBalancesReport(DatabaseContext context) => _context = context;

        public string Key => "customer-balances";
        public ReportScope Scope => ReportScope.Admin;

        public async Task<ReportResultDto> RunAsync(ReportFilter f, ReportContext context, CancellationToken ct)
        {
            var customers = _context.Customers.AsNoTracking().Where(c => !c.IsDeleted);
            if (ReportFilter.Has(f.CityIds)) customers = customers.Where(c => f.CityIds!.Contains(c.CityId));
            if (ReportFilter.Has(f.CustomerIds)) customers = customers.Where(c => f.CustomerIds!.Contains(c.CustomerId));
            var list = await customers
                .OrderBy(c => c.FullName)
                .Select(c => new { c.CustomerId, c.FullName, c.MobileNumber, City = c.City.Name })
                .ToListAsync(ct);
            var ids = list.Select(c => c.CustomerId).ToList();

            var orders = _context.Orders.AsNoTracking().Where(o => ids.Contains(o.CustomerId));
            if (f.From.HasValue) orders = orders.Where(o => o.CreatedDate >= f.From.Value);
            if (f.ToExclusive.HasValue) orders = orders.Where(o => o.CreatedDate < f.ToExclusive.Value);
            var orderSums = (await orders
                .GroupBy(o => o.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Count = g.Count(),
                    Total = g.Where(o => o.OrderState != OrderState.Cancelled).Sum(o => o.OrderTotal),
                    Paid = g.SelectMany(o => o.OrderPayments).Where(p => p.State == PaymentState.Paid).Sum(p => p.Total)
                })
                .ToListAsync(ct)).ToDictionary(x => x.CustomerId);

            var wallets = _context.CustomerWallets.AsNoTracking()
                .Where(w => ids.Contains(w.CustomerId) && w.Type == WalletType.OrderCancellationFees);
            if (f.ToExclusive.HasValue) wallets = wallets.Where(w => w.CreatedDate < f.ToExclusive.Value);
            var fees = (await wallets
                .GroupBy(w => w.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Owed = g.Where(w => w.State != CustomerWalletState.Paid).Sum(w => w.Withdraw - w.Deposit),
                    Paid = g.Where(w => w.State == CustomerWalletState.Paid).Sum(w => w.Withdraw - w.Deposit)
                })
                .ToListAsync(ct)).ToDictionary(x => x.CustomerId);

            var refundsQuery = _context.RefundablePaypalAmounts.AsNoTracking().Where(r => ids.Contains(r.CustomerId));
            if (f.ToExclusive.HasValue) refundsQuery = refundsQuery.Where(r => r.CreatedDate < f.ToExclusive.Value);
            var refunds = (await refundsQuery
                .GroupBy(r => r.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Pending = g.Where(r => r.State == RefundState.Pending).Sum(r => r.RefundableAmount),
                    Done = g.Where(r => r.State == RefundState.Success).Sum(r => r.RefundableAmount)
                })
                .ToListAsync(ct)).ToDictionary(x => x.CustomerId);

            var b = new ReportBuilder(Key, "Customer balances")
                .Text("customer", "Customer").Text("mobile", "Mobile").Text("city", "City")
                .Number("orders", "Orders").Money("ordersTotal", "Orders total").Money("paid", "Paid")
                .Money("feesOwed", "Cancellation fees owed").Money("feesPaid", "Cancellation fees paid")
                .Money("refundsOwed", "Refunds owed to customer").Money("refundsDone", "Refunds done")
                .Money("net", "Net (customer owes)");

            var count = 0;
            foreach (var c in list)
            {
                orderSums.TryGetValue(c.CustomerId, out var o);
                fees.TryGetValue(c.CustomerId, out var fee);
                refunds.TryGetValue(c.CustomerId, out var refund);
                if (o == null && fee == null && refund == null) continue;

                count++;
                var feesOwed = fee?.Owed ?? 0;
                var refundsOwed = refund?.Pending ?? 0;
                b.Row(new()
                {
                    ["customer"] = c.FullName,
                    ["mobile"] = c.MobileNumber,
                    ["city"] = c.City,
                    ["orders"] = o?.Count ?? 0,
                    ["ordersTotal"] = o?.Total ?? 0,
                    ["paid"] = o?.Paid ?? 0,
                    ["feesOwed"] = feesOwed,
                    ["feesPaid"] = fee?.Paid ?? 0,
                    ["refundsOwed"] = refundsOwed,
                    ["refundsDone"] = refund?.Done ?? 0,
                    ["net"] = feesOwed - refundsOwed
                });
            }

            return b.Kpi("feesOwed", "Fees owed by customers", b.Sum("feesOwed"))
                .Kpi("refundsOwed", "Refunds owed to customers", b.Sum("refundsOwed"))
                .Kpi("paid", "Paid in period", b.Sum("paid"))
                .Kpi("customers", "Customers", count, ReportColumnType.Number)
                .Build();
        }
    }
}
