using Domain.Enums;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reports.Admin
{
    /// <summary>Settlement vouchers: cash received from deliveries and paid to merchants / deliveries.</summary>
    public class VouchersReport : IReport
    {
        private readonly DatabaseContext _context;
        public VouchersReport(DatabaseContext context) => _context = context;

        public string Key => "vouchers";
        public ReportScope Scope => ReportScope.Admin;

        public async Task<ReportResultDto> RunAsync(ReportFilter f, ReportContext context, CancellationToken ct)
        {
            var query = _context.SettlementVouchers.AsNoTracking();
            if (f.From.HasValue) query = query.Where(v => v.CreatedDate >= f.From.Value);
            if (f.ToExclusive.HasValue) query = query.Where(v => v.CreatedDate < f.ToExclusive.Value);
            if (ReportFilter.Has(f.Directions)) query = query.Where(v => f.Directions!.Contains(v.Direction));
            if (ReportFilter.Has(f.PartyTypes)) query = query.Where(v => f.PartyTypes!.Contains(v.PartyType));
            if (ReportFilter.Has(f.MerchantIds) || ReportFilter.Has(f.DeliveryIds))
            {
                var merchantIds = f.MerchantIds ?? new List<int>();
                var deliveryIds = f.DeliveryIds ?? new List<int>();
                query = query.Where(v =>
                    (v.PartyType == LedgerPartyType.Merchant && merchantIds.Contains(v.PartyId)) ||
                    (v.PartyType == LedgerPartyType.Delivery && deliveryIds.Contains(v.PartyId)));
            }
            if (context.MerchantId.HasValue)
                query = query.Where(v => v.PartyType == LedgerPartyType.Merchant && v.PartyId == context.MerchantId.Value);

            var rows = await query
                .OrderByDescending(v => v.CreatedDate)
                .Select(v => new
                {
                    v.VoucherNo,
                    v.CreatedDate,
                    v.PartyType,
                    Party = v.PartyType == LedgerPartyType.Delivery
                        ? _context.Deliveries.Where(d => d.DeliveryId == v.PartyId).Select(d => d.FullName).FirstOrDefault()
                        : _context.Merchants.Where(m => m.MerchantId == v.PartyId).Select(m => m.FullName).FirstOrDefault(),
                    v.Direction,
                    v.Amount,
                    v.Note,
                    v.CreatedBy,
                    Orders = v.Allocations.Where(a => a.Order != null).Select(a => a.Order!.OrderCode).Distinct().ToList()
                })
                .ToListAsync(ct);

            var b = new ReportBuilder(Key, "Receipts and payments")
                .Text("voucherNo", "Voucher").DateTime("date", "Date")
                .Badge("partyType", "Party type").Text("party", "Party").Badge("direction", "Voucher type")
                .Money("received", "Received").Money("paid", "Paid").Text("orders", "Orders").Text("note", "Note").Text("createdBy", "By");

            foreach (var v in rows)
            {
                var collect = v.Direction == SettlementDirection.CollectFromParty;
                b.Row(new()
                {
                    ["voucherNo"] = v.VoucherNo,
                    ["date"] = v.CreatedDate,
                    ["partyType"] = v.PartyType.ToString(),
                    ["party"] = v.Party,
                    ["direction"] = v.Direction.ToString(),
                    ["received"] = collect ? v.Amount : 0m,
                    ["paid"] = collect ? 0m : v.Amount,
                    ["orders"] = string.Join(", ", v.Orders),
                    ["note"] = v.Note,
                    ["createdBy"] = v.CreatedBy
                });
            }

            return b.Kpi("vouchers", "Vouchers", rows.Count, ReportColumnType.Number)
                .Kpi("received", "Received", b.Sum("received"))
                .Kpi("paid", "Paid", b.Sum("paid"))
                .Kpi("net", "Net cash", b.Sum("received") - b.Sum("paid"))
                .Build();
        }
    }
}
