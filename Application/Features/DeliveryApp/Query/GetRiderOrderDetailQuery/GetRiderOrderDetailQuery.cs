using Application.Features.Delivery.Common;
using Application.Features.DeliveryApp.Common;
using Application.Features.DeliveryApp.DTOs;
using CSharpFunctionalExtensions;
using Domain.Common;
using Domain.Enums;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.DeliveryApp.Query.GetRiderOrderDetailQuery
{
    public record GetRiderOrderDetailQuery : IRequest<Result<RiderOrderDetailDto>>
    {
        public int OrderId { get; set; }
    }

    public class GetRiderOrderDetailQueryHandler : IRequestHandler<GetRiderOrderDetailQuery, Result<RiderOrderDetailDto>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;

        public GetRiderOrderDetailQueryHandler(DatabaseContext context, IUserSession userSession)
        {
            _context = context;
            _userSession = userSession;
        }

        public async Task<Result<RiderOrderDetailDto>> Handle(GetRiderOrderDetailQuery request, CancellationToken cancellationToken)
        {
            var rider = await RiderContext.GetCurrentAsync(_context, _userSession, cancellationToken);
            if (rider.IsFailure)
                return Result.Failure<RiderOrderDetailDto>(rider.Error);

            var riderId = rider.Value.DeliveryId;
            var order = await RiderOrderReader.MyOrders(_context, riderId)
                .FirstOrDefaultAsync(o => o.OrderId == request.OrderId, cancellationToken);

            // Same message whether it does not exist or belongs to someone else.
            if (order == null)
                return Result.Failure<RiderOrderDetailDto>($"Order {request.OrderId} not found");

            var cashJournals = await _context.OrderJournals
                .AsNoTracking()
                .Where(j => j.OrderId == request.OrderId
                    && j.PartyType == LedgerPartyType.Delivery
                    && j.EntryKind == OrderJournalEntryKind.CashCollectedFromCustomer)
                .ToListAsync(cancellationToken);

            var images = await _context.OrderVehicleHandoverImages
                .AsNoTracking()
                .Where(i => i.OrderId == request.OrderId)
                .ToListAsync(cancellationToken);

            return Result.Success(RiderOrderReader.ToDetail(order, riderId, cashJournals, images));
        }
    }
}
