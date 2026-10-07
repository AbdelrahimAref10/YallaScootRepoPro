using Domain.Enums;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reports.Admin
{
    /// <summary>Customer order payments (cash / PayPal) and their state.</summary>
    public class PaymentsReport : IReport
    {
        private readonly DatabaseContext _context;
        public PaymentsReport(DatabaseContext context) => _context = context;

        public string Key => "payments";
        public ReportScope Scope => ReportScope.Admin;

        public async Task<ReportResultDto> RunAsync(ReportFilter f, ReportContext context, CancellationToken ct)
        {
            var query = _context.OrderPayments.AsNoTracking();
            if (f.From.HasValue) query = query.Where(p => p.CreatedDate >= f.From.Value);
            if (f.ToExclusive.HasValue) query = query.Where(p => p.CreatedDate < f.ToExclusive.Value);
            if (ReportFilter.Has(f.CityIds)) query = query.Where(p => f.CityIds!.Contains(p.Order.CityId));
            if (ReportFilter.Has(f.CustomerIds)) query = query.Where(p => f.CustomerIds!.Contains(p.Order.CustomerId));
            if (ReportFilter.Has(f.PaymentStates)) query = query.Where(p => f.PaymentStates!.Contains(p.State));
            if (ReportFilter.Has(f.PaymentMethods))
            {
                var methods = f.PaymentMethods!.Select(m => (int)m).ToList();
                query = query.Where(p => methods.Contains(p.PaymentMethodId));
            }

            var rows = await query
                .OrderByDescending(p => p.CreatedDate)
                .Select(p => new
                {
                    p.Order.OrderCode,
                    p.CreatedDate,
                    Customer = p.Order.Customer.FullName,
                    City = p.Order.City.Name,
                    p.PaymentMethodId,
                    p.State,
                    p.Total
                })
                .ToListAsync(ct);

            var b = new ReportBuilder(Key, "Customer payments")
                .Text("orderCode", "Order").DateTime("date", "Date").Text("customer", "Customer").Text("city", "City")
                .Badge("paymentMethod", "Method").Badge("paymentState", "Status").Money("amount", "Amount");

            foreach (var p in rows)
            {
                b.Row(new()
                {
                    ["orderCode"] = p.OrderCode,
                    ["date"] = p.CreatedDate,
                    ["customer"] = p.Customer,
                    ["city"] = p.City,
                    ["paymentMethod"] = ((PaymentMethod)p.PaymentMethodId).ToString(),
                    ["paymentState"] = p.State.ToString(),
                    ["amount"] = p.Total
                });
            }

            decimal PaidBy(PaymentMethod method) => rows.Where(r => r.State == PaymentState.Paid && r.PaymentMethodId == (int)method).Sum(r => r.Total);
            return b.Kpi("paid", "Paid", rows.Where(r => r.State == PaymentState.Paid).Sum(r => r.Total))
                .Kpi("cashPaid", "Cash paid", PaidBy(PaymentMethod.Cash))
                .Kpi("paypalPaid", "PayPal paid", PaidBy(PaymentMethod.PayPal))
                .Kpi("pending", "Pending", rows.Where(r => r.State == PaymentState.Pending).Sum(r => r.Total))
                .Build();
        }
    }
}
