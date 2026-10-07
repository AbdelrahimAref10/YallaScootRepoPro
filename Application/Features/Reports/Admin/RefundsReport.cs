using Domain.Enums;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reports.Admin
{
    /// <summary>PayPal amounts to refund to customers after cancellations.</summary>
    public class RefundsReport : IReport
    {
        private readonly DatabaseContext _context;
        public RefundsReport(DatabaseContext context) => _context = context;

        public string Key => "refunds";
        public ReportScope Scope => ReportScope.Admin;

        public async Task<ReportResultDto> RunAsync(ReportFilter f, ReportContext context, CancellationToken ct)
        {
            var query = _context.RefundablePaypalAmounts.AsNoTracking();
            if (f.From.HasValue) query = query.Where(r => r.CreatedDate >= f.From.Value);
            if (f.ToExclusive.HasValue) query = query.Where(r => r.CreatedDate < f.ToExclusive.Value);
            if (ReportFilter.Has(f.CityIds)) query = query.Where(r => f.CityIds!.Contains(r.Order.CityId));
            if (ReportFilter.Has(f.CustomerIds)) query = query.Where(r => f.CustomerIds!.Contains(r.CustomerId));
            if (ReportFilter.Has(f.RefundStates)) query = query.Where(r => f.RefundStates!.Contains(r.State));

            var rows = await query.OrderByDescending(r => r.CreatedDate)
                .Select(r => new
                {
                    r.Order.OrderCode,
                    r.CreatedDate,
                    Customer = r.Customer.FullName,
                    r.Customer.MobileNumber,
                    r.OrderTotal,
                    r.CancellationFees,
                    r.RefundableAmount,
                    r.State
                })
                .ToListAsync(ct);

            var b = new ReportBuilder(Key, "PayPal refunds")
                .Text("orderCode", "Order").DateTime("date", "Date").Text("customer", "Customer").Text("mobile", "Mobile")
                .Money("orderTotal", "Order total").Money("cancellationFees", "Cancellation fees").Money("refundable", "Refundable")
                .Badge("refundState", "Status");

            foreach (var r in rows)
            {
                b.Row(new()
                {
                    ["orderCode"] = r.OrderCode,
                    ["date"] = r.CreatedDate,
                    ["customer"] = r.Customer,
                    ["mobile"] = r.MobileNumber,
                    ["orderTotal"] = r.OrderTotal,
                    ["cancellationFees"] = r.CancellationFees,
                    ["refundable"] = r.RefundableAmount,
                    ["refundState"] = $"Refund{r.State}"
                });
            }

            return b.Kpi("refundable", "Refundable", b.Sum("refundable"))
                .Kpi("refundPending", "Pending", rows.Where(r => r.State == RefundState.Pending).Sum(r => r.RefundableAmount))
                .Kpi("refundDone", "Refunded", rows.Where(r => r.State == RefundState.Success).Sum(r => r.RefundableAmount))
                .Build();
        }
    }
}
