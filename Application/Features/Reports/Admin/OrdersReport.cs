using Domain.Enums;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reports.Admin
{
    /// <summary>Every order with its money: what the customer pays, what merchants and riders earn, what the company keeps.</summary>
    public class OrdersReport : IReport
    {
        private readonly DatabaseContext _context;
        public OrdersReport(DatabaseContext context) => _context = context;

        public string Key => "orders";
        public ReportScope Scope => ReportScope.Admin;

        public async Task<ReportResultDto> RunAsync(ReportFilter f, ReportContext context, CancellationToken ct)
        {
            var query = _context.Orders.AsNoTracking();
            if (f.From.HasValue) query = query.Where(o => o.CreatedDate >= f.From.Value);
            if (f.ToExclusive.HasValue) query = query.Where(o => o.CreatedDate < f.ToExclusive.Value);
            if (ReportFilter.Has(f.CityIds)) query = query.Where(o => f.CityIds!.Contains(o.CityId));
            if (ReportFilter.Has(f.CustomerIds)) query = query.Where(o => f.CustomerIds!.Contains(o.CustomerId));
            if (ReportFilter.Has(f.OrderStates)) query = query.Where(o => f.OrderStates!.Contains(o.OrderState));
            if (ReportFilter.Has(f.PaymentMethods))
            {
                var methods = f.PaymentMethods!.Select(m => (int)m).ToList();
                query = query.Where(o => methods.Contains(o.PaymentMethodId));
            }
            if (ReportFilter.Has(f.MerchantIds)) query = query.Where(o => o.OrderVehicles.Any(v => f.MerchantIds!.Contains(v.Vehicle.MerchantId)));
            if (ReportFilter.Has(f.DeliveryIds)) query = query.Where(o => o.DeliveryMenOrders.Any(d => f.DeliveryIds!.Contains(d.DeliveryId)));

            var orders = await query
                .OrderByDescending(o => o.CreatedDate)
                .Select(o => new
                {
                    o.OrderId,
                    o.OrderCode,
                    o.CreatedDate,
                    Customer = o.Customer.FullName,
                    City = o.City.Name,
                    o.OrderState,
                    o.PaymentMethodId,
                    PaymentState = o.OrderPayments.OrderByDescending(p => p.Id).Select(p => (PaymentState?)p.State).FirstOrDefault(),
                    o.VehiclesCount,
                    o.OrderSubTotal,
                    o.OrderDeliveryFees,
                    o.OrderServiceFees,
                    o.OrderUrgentFees,
                    o.OrderTieredDiscount,
                    o.PreviousDebt,
                    o.OrderTotal
                })
                .ToListAsync(ct);

            var ids = orders.Select(o => o.OrderId).ToList();
            var journal = await _context.OrderJournals.AsNoTracking()
                .Where(j => j.OrderId != null && ids.Contains(j.OrderId.Value))
                .GroupBy(j => new { OrderId = j.OrderId!.Value, j.PartyType, j.EntryKind, j.Direction })
                .Select(g => new { g.Key.OrderId, g.Key.PartyType, g.Key.EntryKind, g.Key.Direction, Amount = g.Sum(x => x.Amount) })
                .ToListAsync(ct);
            var byOrder = journal.ToLookup(j => j.OrderId);

            decimal Sum(int orderId, LedgerPartyType party, JournalDirection direction, params OrderJournalEntryKind[] kinds) =>
                byOrder[orderId].Where(j => j.PartyType == party && j.Direction == direction && kinds.Contains(j.EntryKind)).Sum(j => j.Amount);

            var b = new ReportBuilder(Key, "Orders details")
                .Text("orderCode", "Order").DateTime("date", "Date").Text("customer", "Customer").Text("city", "City")
                .Badge("orderState", "Status").Badge("paymentMethod", "Payment").Badge("paymentState", "Payment status")
                .Number("vehicles", "Vehicles")
                .Money("subTotal", "Rental").Money("deliveryFees", "Delivery fees").Money("serviceFees", "Service fees")
                .Money("urgentFees", "Urgent fees").Money("discount", "Discount").Money("previousDebt", "Previous debt")
                .Money("total", "Order total")
                .Money("merchantsNet", "Merchants net").Money("ridersCommission", "Riders commission").Money("companyIncome", "Company income");

            foreach (var o in orders)
            {
                var merchantsNet = Sum(o.OrderId, LedgerPartyType.Merchant, JournalDirection.Credit, OrderJournalEntryKind.MerchantRentalAccrued)
                                   - Sum(o.OrderId, LedgerPartyType.Merchant, JournalDirection.Debit, OrderJournalEntryKind.MerchantCompanyCommissionCharged);
                var ridersCommission = Sum(o.OrderId, LedgerPartyType.Delivery, JournalDirection.Credit, OrderJournalEntryKind.DeliveryFeeAccrued);
                var companyIncome = Sum(o.OrderId, LedgerPartyType.Company, JournalDirection.Credit,
                                        OrderJournalEntryKind.CompanyServiceFeeAccrued,
                                        OrderJournalEntryKind.CompanyDeliveryFeeRemainderAccrued,
                                        OrderJournalEntryKind.CompanyMerchantCommissionAccrued)
                                    - Sum(o.OrderId, LedgerPartyType.Company, JournalDirection.Debit, OrderJournalEntryKind.CompanyReturnLegCommissionCharged);

                b.Row(new()
                {
                    ["orderCode"] = o.OrderCode,
                    ["date"] = o.CreatedDate,
                    ["customer"] = o.Customer,
                    ["city"] = o.City,
                    ["orderState"] = o.OrderState.ToString(),
                    ["paymentMethod"] = ((PaymentMethod)o.PaymentMethodId).ToString(),
                    ["paymentState"] = o.PaymentState?.ToString(),
                    ["vehicles"] = o.VehiclesCount,
                    ["subTotal"] = o.OrderSubTotal,
                    ["deliveryFees"] = o.OrderDeliveryFees,
                    ["serviceFees"] = o.OrderServiceFees,
                    ["urgentFees"] = o.OrderUrgentFees,
                    ["discount"] = o.OrderTieredDiscount,
                    ["previousDebt"] = o.PreviousDebt,
                    ["total"] = o.OrderTotal,
                    ["merchantsNet"] = merchantsNet,
                    ["ridersCommission"] = ridersCommission,
                    ["companyIncome"] = companyIncome
                });
            }

            var paid = orders.Where(o => o.PaymentState == PaymentState.Paid).Sum(o => o.OrderTotal);
            return b.Kpi("orders", "Orders", orders.Count, ReportColumnType.Number)
                .Kpi("ordersTotal", "Orders total", b.Sum("total"))
                .Kpi("paid", "Paid", paid)
                .Kpi("companyIncome", "Company income", b.Sum("companyIncome"))
                .Build();
        }
    }
}
