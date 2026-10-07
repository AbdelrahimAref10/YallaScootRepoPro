using Domain.Enums;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reports
{
    /// <summary>Journal sums per party, split into "before the period" (opening) and "in the period".</summary>
    internal static class ReportJournal
    {
        public sealed record Sum(int PartyId, int? OrderId, OrderJournalEntryKind Kind, JournalDirection Direction, bool BeforePeriod, decimal Amount);

        public static async Task<List<Sum>> SumsAsync(
            DatabaseContext context, LedgerPartyType partyType, ICollection<int>? partyIds, ReportFilter filter,
            bool perOrder, CancellationToken cancellationToken)
        {
            var from = filter.From;
            var to = filter.ToExclusive;

            var query = context.OrderJournals.AsNoTracking().Where(j => j.PartyType == partyType && j.PartyId != null);
            if (partyIds != null)
                query = query.Where(j => partyIds.Contains(j.PartyId!.Value));
            if (to.HasValue)
                query = query.Where(j => j.CreatedDate < to.Value);

            var rows = await query
                .GroupBy(j => new
                {
                    PartyId = j.PartyId!.Value,
                    OrderId = perOrder ? j.OrderId : null,
                    j.EntryKind,
                    j.Direction,
                    Before = from.HasValue && j.CreatedDate < from.Value
                })
                .Select(g => new { g.Key.PartyId, g.Key.OrderId, g.Key.EntryKind, g.Key.Direction, g.Key.Before, Amount = g.Sum(x => x.Amount) })
                .ToListAsync(cancellationToken);

            return rows.Select(r => new Sum(r.PartyId, r.OrderId, r.EntryKind, r.Direction, r.Before, r.Amount)).ToList();
        }

        public static decimal Total(IEnumerable<Sum> sums, OrderJournalEntryKind kind, JournalDirection direction, bool? beforePeriod = false) =>
            sums.Where(s => s.Kind == kind && s.Direction == direction && (beforePeriod == null || s.BeforePeriod == beforePeriod)).Sum(s => s.Amount);

        /// <summary>Σ credit − Σ debit (positive = the company owes the party).</summary>
        public static decimal Balance(IEnumerable<Sum> sums, bool? beforePeriod = null) =>
            sums.Where(s => beforePeriod == null || s.BeforePeriod == beforePeriod)
                .Sum(s => s.Direction == JournalDirection.Credit ? s.Amount : -s.Amount);
    }
}
