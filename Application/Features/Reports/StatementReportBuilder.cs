using Domain.Enums;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reports
{
    /// <summary>Journal lines of merchants / deliveries with an opening row and a running balance per party.</summary>
    internal static class StatementReportBuilder
    {
        public sealed record Party(LedgerPartyType Type, int Id, string Name);

        public static async Task<ReportResultDto> BuildAsync(
            DatabaseContext context, string key, IReadOnlyCollection<Party> parties, ReportFilter f, CancellationToken ct)
        {
            var b = new ReportBuilder(key, "Account statement")
                .Text("party", "Party").DateTime("date", "Date").Text("orderCode", "Order")
                .Badge("entry", "Movement", "orders.journalKind.").Text("note", "Note")
                .Money("debit", "Debit").Money("credit", "Credit").Money("balance", "Balance", total: false);

            decimal openingTotal = 0, closingTotal = 0;
            foreach (var group in parties.GroupBy(p => p.Type))
            {
                var ids = group.Select(p => p.Id).ToList();
                var journal = context.OrderJournals.AsNoTracking()
                    .Where(j => j.PartyType == group.Key && j.PartyId != null && ids.Contains(j.PartyId.Value));

                var openings = f.From.HasValue
                    ? await journal.Where(j => j.CreatedDate < f.From.Value)
                        .GroupBy(j => j.PartyId!.Value)
                        .Select(g => new { PartyId = g.Key, Balance = g.Sum(j => j.Direction == JournalDirection.Credit ? j.Amount : -j.Amount) })
                        .ToDictionaryAsync(x => x.PartyId, x => x.Balance, ct)
                    : new Dictionary<int, decimal>();

                var lines = journal;
                if (f.From.HasValue) lines = lines.Where(j => j.CreatedDate >= f.From.Value);
                if (f.ToExclusive.HasValue) lines = lines.Where(j => j.CreatedDate < f.ToExclusive.Value);
                var rows = (await lines
                    .OrderBy(j => j.CreatedDate).ThenBy(j => j.OrderJournalId)
                    .Select(j => new
                    {
                        PartyId = j.PartyId!.Value,
                        j.CreatedDate,
                        OrderCode = j.Order != null ? j.Order.OrderCode : null,
                        j.EntryKind,
                        j.Note,
                        j.Direction,
                        j.Amount
                    })
                    .ToListAsync(ct)).ToLookup(r => r.PartyId);

                foreach (var party in group.OrderBy(p => p.Name))
                {
                    var balance = openings.GetValueOrDefault(party.Id);
                    var partyRows = rows[party.Id].ToList();
                    if (partyRows.Count == 0 && balance == 0)
                        continue;

                    openingTotal += balance;
                    b.Row(new()
                    {
                        ["party"] = party.Name,
                        ["date"] = f.From,
                        ["entry"] = "OpeningBalance",
                        ["debit"] = 0m,
                        ["credit"] = 0m,
                        ["balance"] = balance
                    });

                    foreach (var r in partyRows)
                    {
                        var credit = r.Direction == JournalDirection.Credit ? r.Amount : 0m;
                        var debit = r.Direction == JournalDirection.Debit ? r.Amount : 0m;
                        balance += credit - debit;
                        b.Row(new()
                        {
                            ["party"] = party.Name,
                            ["date"] = r.CreatedDate,
                            ["orderCode"] = r.OrderCode,
                            ["entry"] = r.EntryKind.ToString(),
                            ["note"] = r.Note,
                            ["debit"] = debit,
                            ["credit"] = credit,
                            ["balance"] = balance
                        });
                    }
                    closingTotal += balance;
                }
            }

            return b.Kpi("opening", "Opening balance", openingTotal)
                .Kpi("credit", "Credit (owed to party)", b.Sum("credit"))
                .Kpi("debit", "Debit (owed by party)", b.Sum("debit"))
                .Kpi("closing", "Closing balance", closingTotal)
                .Build();
        }
    }
}
