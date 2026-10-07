using Domain.Enums;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reports.Admin
{
    /// <summary>Account statement of merchants and / or deliveries (running balance; positive = owed to the party).</summary>
    public class StatementReport : IReport
    {
        private readonly DatabaseContext _context;
        public StatementReport(DatabaseContext context) => _context = context;

        public string Key => "statement";
        public ReportScope Scope => ReportScope.Admin;

        public async Task<ReportResultDto> RunAsync(ReportFilter f, ReportContext context, CancellationToken ct)
        {
            var pickedMerchants = ReportFilter.Has(f.MerchantIds);
            var pickedDeliveries = ReportFilter.Has(f.DeliveryIds);
            var types = ReportFilter.Has(f.PartyTypes)
                ? f.PartyTypes!
                : pickedMerchants || pickedDeliveries
                    ? new List<LedgerPartyType>().Concat(pickedMerchants ? new[] { LedgerPartyType.Merchant } : Array.Empty<LedgerPartyType>())
                        .Concat(pickedDeliveries ? new[] { LedgerPartyType.Delivery } : Array.Empty<LedgerPartyType>()).ToList()
                    : new List<LedgerPartyType> { LedgerPartyType.Merchant, LedgerPartyType.Delivery };

            var parties = new List<StatementReportBuilder.Party>();
            if (types.Contains(LedgerPartyType.Merchant))
            {
                var merchants = _context.Merchants.AsNoTracking();
                if (pickedMerchants) merchants = merchants.Where(m => f.MerchantIds!.Contains(m.MerchantId));
                if (ReportFilter.Has(f.CityIds)) merchants = merchants.Where(m => f.CityIds!.Contains(m.CityId));
                parties.AddRange((await merchants.Select(m => new { m.MerchantId, m.FullName }).ToListAsync(ct))
                    .Select(m => new StatementReportBuilder.Party(LedgerPartyType.Merchant, m.MerchantId, m.FullName)));
            }
            if (types.Contains(LedgerPartyType.Delivery))
            {
                var deliveries = _context.Deliveries.AsNoTracking();
                if (pickedDeliveries) deliveries = deliveries.Where(d => f.DeliveryIds!.Contains(d.DeliveryId));
                if (ReportFilter.Has(f.CityIds)) deliveries = deliveries.Where(d => f.CityIds!.Contains(d.CityId));
                parties.AddRange((await deliveries.Select(d => new { d.DeliveryId, d.FullName }).ToListAsync(ct))
                    .Select(d => new StatementReportBuilder.Party(LedgerPartyType.Delivery, d.DeliveryId, d.FullName)));
            }

            return await StatementReportBuilder.BuildAsync(_context, Key, parties, f, ct);
        }
    }
}
