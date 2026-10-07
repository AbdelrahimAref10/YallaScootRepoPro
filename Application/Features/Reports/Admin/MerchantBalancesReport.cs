using Domain.Enums;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using K = Domain.Enums.OrderJournalEntryKind;
using D = Domain.Enums.JournalDirection;

namespace Application.Features.Reports.Admin
{
    /// <summary>What each merchant earned, was charged and was paid in the period, and what is still owed.</summary>
    public class MerchantBalancesReport : IReport
    {
        private readonly DatabaseContext _context;
        public MerchantBalancesReport(DatabaseContext context) => _context = context;

        public string Key => "merchant-balances";
        public ReportScope Scope => ReportScope.Admin;

        public async Task<ReportResultDto> RunAsync(ReportFilter f, ReportContext context, CancellationToken ct)
        {
            var merchants = _context.Merchants.AsNoTracking().Where(m => !m.IsDeleted);
            if (ReportFilter.Has(f.CityIds)) merchants = merchants.Where(m => f.CityIds!.Contains(m.CityId));
            if (ReportFilter.Has(f.MerchantIds)) merchants = merchants.Where(m => f.MerchantIds!.Contains(m.MerchantId));
            var list = await merchants
                .OrderBy(m => m.FullName)
                .Select(m => new { m.MerchantId, m.FullName, m.MobileNumber, City = m.City.Name })
                .ToListAsync(ct);

            var sums = (await ReportJournal.SumsAsync(_context, LedgerPartyType.Merchant, list.Select(m => m.MerchantId).ToList(), f, false, ct))
                .ToLookup(s => s.PartyId);

            var b = new ReportBuilder(Key, "Merchant balances")
                .Text("merchant", "Merchant").Text("mobile", "Mobile").Text("city", "City")
                .Money("opening", "Opening balance")
                .Money("earned", "Rental earned").Money("commission", "Company commission")
                .Money("paid", "Paid by company").Money("deductions", "Deductions")
                .Money("closing", "Closing balance")
                .Money("owedToMerchant", "Owed to merchant").Money("owedByMerchant", "Owed by merchant");

            foreach (var m in list)
            {
                var s = sums[m.MerchantId].ToList();
                var opening = ReportJournal.Balance(s, beforePeriod: true);
                var closing = ReportJournal.Balance(s);
                var earned = ReportJournal.Total(s, K.MerchantRentalAccrued, D.Credit);
                var commission = ReportJournal.Total(s, K.MerchantCompanyCommissionCharged, D.Debit);
                var paid = ReportJournal.Total(s, K.MerchantPaidByCompany, D.Debit);
                var periodDebits = s.Where(x => !x.BeforePeriod && x.Direction == D.Debit).Sum(x => x.Amount);
                if (s.Count == 0) continue;

                b.Row(new()
                {
                    ["merchant"] = m.FullName,
                    ["mobile"] = m.MobileNumber,
                    ["city"] = m.City,
                    ["opening"] = opening,
                    ["earned"] = earned,
                    ["commission"] = commission,
                    ["paid"] = paid,
                    ["deductions"] = periodDebits - commission - paid,
                    ["closing"] = closing,
                    ["owedToMerchant"] = Math.Max(closing, 0),
                    ["owedByMerchant"] = Math.Max(-closing, 0)
                });
            }

            return b.Kpi("owedToMerchants", "Owed to merchants", b.Sum("owedToMerchant"))
                .Kpi("owedByMerchants", "Owed by merchants", b.Sum("owedByMerchant"))
                .Kpi("paidToMerchants", "Paid in period", b.Sum("paid"))
                .Kpi("companyCommission", "Company commission", b.Sum("commission"))
                .Build();
        }
    }
}
