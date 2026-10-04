using Application.Common;
using Application.Features.Delivery.Common;
using Application.Features.DeliveryApp.Common;
using Application.Features.DeliveryApp.DTOs;
using CSharpFunctionalExtensions;
using Domain.Common;
using Domain.Enums;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.DeliveryApp.Query.GetRiderOrdersQuery
{
    /// <summary>
    /// Pickup: I still have a delivery trip to finish. Return: no delivery trip left, but a return trip is open
    /// (including returns waiting on another rider's delivery). Completed: all my trips on the order are done.
    /// </summary>
    public record GetRiderOrdersQuery : IRequest<Result<PagedResult<RiderOrderSummaryDto>>>
    {
        public RiderOrderTab Tab { get; set; } = RiderOrderTab.Pickup;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class GetRiderOrdersQueryHandler : IRequestHandler<GetRiderOrdersQuery, Result<PagedResult<RiderOrderSummaryDto>>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;

        public GetRiderOrdersQueryHandler(DatabaseContext context, IUserSession userSession)
        {
            _context = context;
            _userSession = userSession;
        }

        public async Task<Result<PagedResult<RiderOrderSummaryDto>>> Handle(GetRiderOrdersQuery request, CancellationToken cancellationToken)
        {
            var rider = await RiderContext.GetCurrentAsync(_context, _userSession, cancellationToken);
            if (rider.IsFailure)
                return Result.Failure<PagedResult<RiderOrderSummaryDto>>(rider.Error);

            var riderId = rider.Value.DeliveryId;
            var pageNumber = Math.Max(1, request.PageNumber);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            var skip = (pageNumber - 1) * pageSize;

            // Open orders are few: load them and classify by my legs in memory.
            var openOrders = await RiderOrderReader.MyOrders(_context, riderId)
                .Where(o => o.OrderState != OrderState.Completed
                    && o.OrderState != OrderState.Cancelled
                    && o.OrderState != OrderState.CustomerRejectedReceipt)
                .OrderBy(o => o.ReservationDateFrom)
                .ToListAsync(cancellationToken);

            var inTab = openOrders
                .Where(o => RiderOrderReader.TabFor(o, o.DeliveryMenOrders.Where(d => d.DeliveryId == riderId).ToList()) == request.Tab)
                .ToList();

            List<Domain.Models.Order> page;
            int totalCount;

            if (request.Tab == RiderOrderTab.Completed)
            {
                // Completed = open orders where my legs are done (newest first) + finished orders, paged in SQL.
                inTab = inTab.OrderByDescending(o => o.LastModifiedDate).ToList();
                var finished = RiderOrderReader.MyOrders(_context, riderId)
                    .Where(o => o.OrderState == OrderState.Completed
                        || o.OrderState == OrderState.Cancelled
                        || o.OrderState == OrderState.CustomerRejectedReceipt)
                    .OrderByDescending(o => o.LastModifiedDate);

                var finishedCount = await finished.CountAsync(cancellationToken);
                totalCount = inTab.Count + finishedCount;

                page = inTab.Skip(skip).Take(pageSize).ToList();
                if (page.Count < pageSize)
                {
                    var finishedSkip = Math.Max(0, skip - inTab.Count);
                    page.AddRange(await finished
                        .Skip(finishedSkip)
                        .Take(pageSize - page.Count)
                        .ToListAsync(cancellationToken));
                }
            }
            else
            {
                if (request.Tab == RiderOrderTab.Return)
                    inTab = inTab.OrderBy(o => o.ReservationDateTo).ToList();

                totalCount = inTab.Count;
                page = inTab.Skip(skip).Take(pageSize).ToList();
            }

            var pageIds = page.Select(o => o.OrderId).ToList();

            var cashJournals = await _context.OrderJournals
                .AsNoTracking()
                .Where(j => j.OrderId != null
                    && pageIds.Contains(j.OrderId.Value)
                    && j.PartyType == LedgerPartyType.Delivery
                    && j.EntryKind == OrderJournalEntryKind.CashCollectedFromCustomer)
                .ToListAsync(cancellationToken);

            return Result.Success(new PagedResult<RiderOrderSummaryDto>
            {
                Items = page.Select(o => RiderOrderReader.ToSummary(o, riderId, cashJournals)).ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }
    }
}
