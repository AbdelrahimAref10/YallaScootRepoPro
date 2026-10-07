using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reports.Admin
{
    /// <summary>Company cash book: money in and out with a running balance.</summary>
    public class TreasuryReport : IReport
    {
        private readonly DatabaseContext _context;
        public TreasuryReport(DatabaseContext context) => _context = context;

        public string Key => "treasury";
        public ReportScope Scope => ReportScope.Admin;

        public async Task<ReportResultDto> RunAsync(ReportFilter f, ReportContext context, CancellationToken ct)
        {
            var opening = f.From.HasValue
                ? await _context.CompanyTreasuries.AsNoTracking().Where(t => t.CreatedDate < f.From.Value).SumAsync(t => t.DebitAmount - t.CreditAmount, ct)
                : 0m;

            var query = _context.CompanyTreasuries.AsNoTracking();
            if (f.From.HasValue) query = query.Where(t => t.CreatedDate >= f.From.Value);
            if (f.ToExclusive.HasValue) query = query.Where(t => t.CreatedDate < f.ToExclusive.Value);
            var rows = await query.OrderBy(t => t.CreatedDate).ThenBy(t => t.Id)
                .Select(t => new { t.CreatedDate, t.DescriptionEng, t.DebitAmount, t.CreditAmount, t.CreatedBy })
                .ToListAsync(ct);

            var b = new ReportBuilder(Key, "Company treasury")
                .DateTime("date", "Date").Text("description", "Description")
                .Money("moneyIn", "Money in").Money("moneyOut", "Money out").Money("balance", "Balance", total: false)
                .Text("createdBy", "By");

            var balance = opening;
            foreach (var t in rows)
            {
                balance += t.DebitAmount - t.CreditAmount;
                b.Row(new()
                {
                    ["date"] = t.CreatedDate,
                    ["description"] = t.DescriptionEng,
                    ["moneyIn"] = t.DebitAmount,
                    ["moneyOut"] = t.CreditAmount,
                    ["balance"] = balance,
                    ["createdBy"] = t.CreatedBy
                });
            }

            return b.Kpi("opening", "Opening balance", opening)
                .Kpi("moneyIn", "Money in", b.Sum("moneyIn"))
                .Kpi("moneyOut", "Money out", b.Sum("moneyOut"))
                .Kpi("closing", "Closing balance", balance)
                .Build();
        }
    }
}
