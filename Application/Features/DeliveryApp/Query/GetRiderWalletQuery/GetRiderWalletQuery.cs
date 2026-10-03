using Application.Common;
using Application.Features.Delivery.Common;
using Application.Features.DeliveryApp.DTOs;
using CSharpFunctionalExtensions;
using Domain.Common;
using Domain.Enums;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.DeliveryApp.Query.GetRiderWalletQuery
{
    /// <summary>
    /// Rider money from the order ledger: cash collected from customers that he still owes the company,
    /// and commission the company still owes him. Riders hold no cash float.
    /// </summary>
    public record GetRiderWalletQuery : IRequest<Result<RiderWalletDto>>;

    public record GetRiderJournalsQuery : IRequest<Result<PagedResult<RiderJournalDto>>>
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 30;
    }

    public class GetRiderWalletQueryHandler :
        IRequestHandler<GetRiderWalletQuery, Result<RiderWalletDto>>,
        IRequestHandler<GetRiderJournalsQuery, Result<PagedResult<RiderJournalDto>>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;

        public GetRiderWalletQueryHandler(DatabaseContext context, IUserSession userSession)
        {
            _context = context;
            _userSession = userSession;
        }

        public async Task<Result<RiderWalletDto>> Handle(GetRiderWalletQuery request, CancellationToken cancellationToken)
        {
            var rider = await RiderContext.GetCurrentAsync(_context, _userSession, cancellationToken);
            if (rider.IsFailure)
                return Result.Failure<RiderWalletDto>(rider.Error);

            var sums = await _context.OrderJournals
                .AsNoTracking()
                .Where(j => j.PartyType == LedgerPartyType.Delivery && j.PartyId == rider.Value.DeliveryId)
                .GroupBy(j => new { j.EntryKind, j.Direction })
                .Select(g => new { g.Key.EntryKind, g.Key.Direction, Amount = g.Sum(x => x.Amount) })
                .ToListAsync(cancellationToken);

            decimal Sum(OrderJournalEntryKind kind, JournalDirection direction) =>
                sums.Where(s => s.EntryKind == kind && s.Direction == direction).Sum(s => s.Amount);

            var collected = Sum(OrderJournalEntryKind.CashCollectedFromCustomer, JournalDirection.Debit);
            var remitted = Sum(OrderJournalEntryKind.DeliveryRemittanceToCompany, JournalDirection.Credit);
            var earned = Sum(OrderJournalEntryKind.DeliveryFeeAccrued, JournalDirection.Credit);
            var paid = Sum(OrderJournalEntryKind.DeliveryPaidByCompany, JournalDirection.Debit);
            var clawbacks = Sum(OrderJournalEntryKind.FaultClawback, JournalDirection.Debit)
                + Sum(OrderJournalEntryKind.NonDeliveryFaultDebit, JournalDirection.Debit);

            // Lines from before riders stopped handling merchant cash / floats still count toward cash owed.
            var legacyCashOwed = Sum(OrderJournalEntryKind.DeliveryCashFloatReceived, JournalDirection.Debit)
                - Sum(OrderJournalEntryKind.DeliveryCashFloatReturned, JournalDirection.Credit)
                - Sum(OrderJournalEntryKind.DeliveryCashAdvanceToMerchant, JournalDirection.Credit);

            var cashDebt = collected - remitted + legacyCashOwed;

            var credits = sums.Where(s => s.Direction == JournalDirection.Credit).Sum(s => s.Amount);
            var debits = sums.Where(s => s.Direction == JournalDirection.Debit).Sum(s => s.Amount);

            return Result.Success(new RiderWalletDto
            {
                CashCollected = collected,
                CashRemitted = remitted,
                CashDebt = cashDebt,
                CashDebtLimit = rider.Value.CashDebtLimit,
                IsOverCashDebtLimit = RiderCashDebt.IsOverLimit(cashDebt, rider.Value.CashDebtLimit),
                CommissionEarned = earned,
                CommissionPaid = paid,
                Deductions = clawbacks,
                CommissionDue = earned - paid - clawbacks,
                Balance = credits - debits
            });
        }

        public async Task<Result<PagedResult<RiderJournalDto>>> Handle(GetRiderJournalsQuery request, CancellationToken cancellationToken)
        {
            var rider = await RiderContext.GetCurrentAsync(_context, _userSession, cancellationToken);
            if (rider.IsFailure)
                return Result.Failure<PagedResult<RiderJournalDto>>(rider.Error);

            var pageNumber = Math.Max(1, request.PageNumber);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            var query = _context.OrderJournals
                .AsNoTracking()
                .Where(j => j.PartyType == LedgerPartyType.Delivery && j.PartyId == rider.Value.DeliveryId);

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(j => j.CreatedDate)
                .ThenByDescending(j => j.OrderJournalId)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(j => new RiderJournalDto
                {
                    Id = j.OrderJournalId,
                    OrderId = j.OrderId,
                    OrderCode = j.Order != null ? j.Order.OrderCode : null,
                    EntryKind = j.EntryKind,
                    Amount = j.Direction == JournalDirection.Credit ? j.Amount : -j.Amount,
                    Note = j.Note,
                    CreatedDate = j.CreatedDate
                })
                .ToListAsync(cancellationToken);

            return Result.Success(new PagedResult<RiderJournalDto>
            {
                Items = items,
                TotalCount = total,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }
    }
}
