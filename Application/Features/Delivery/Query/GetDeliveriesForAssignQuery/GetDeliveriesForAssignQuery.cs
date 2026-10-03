using Application.Features.Delivery.Common;
using CSharpFunctionalExtensions;
using Domain.Enums;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Delivery.Query.GetDeliveriesForAssignQuery
{
    public class DeliveryAssignCandidateDto
    {
        public int DeliveryId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public int ZoneId { get; set; }
        public string ZoneName { get; set; } = string.Empty;

        /// <summary>1 = Available (in shift, online), 2 = In shift but offline, 3 = Off shift.</summary>
        public RiderAvailabilityStatus Status { get; set; }
        public bool IsOnline { get; set; }
        public bool IsInShift { get; set; }
        public string? CurrentShiftName { get; set; }

        /// <summary>UTC end of the running shift.</summary>
        public DateTime? CurrentShiftEndsAt { get; set; }

        /// <summary>Unfinished legs the rider holds on other orders.</summary>
        public int ActiveLegsCount { get; set; }
    }

    /// <summary>
    /// Riders of the order's city for the assign / reassign dialog, best first:
    /// in shift and online, then in shift but offline, then off shift; fewer open legs first.
    /// </summary>
    public record GetDeliveriesForAssignQuery : IRequest<Result<List<DeliveryAssignCandidateDto>>>
    {
        public int? OrderId { get; set; }
        public int? CityId { get; set; }
    }

    public class GetDeliveriesForAssignQueryHandler
        : IRequestHandler<GetDeliveriesForAssignQuery, Result<List<DeliveryAssignCandidateDto>>>
    {
        private readonly DatabaseContext _context;

        public GetDeliveriesForAssignQueryHandler(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<Result<List<DeliveryAssignCandidateDto>>> Handle(
            GetDeliveriesForAssignQuery request,
            CancellationToken cancellationToken)
        {
            var cityId = request.CityId;
            if (request.OrderId.HasValue)
            {
                cityId = await _context.Orders
                    .AsNoTracking()
                    .Where(o => o.OrderId == request.OrderId.Value)
                    .Select(o => (int?)o.CityId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (cityId == null)
                    return Result.Failure<List<DeliveryAssignCandidateDto>>($"Order with ID {request.OrderId} not found");
            }

            if (cityId == null)
                return Result.Failure<List<DeliveryAssignCandidateDto>>("OrderId or CityId is required");

            var riders = await _context.Deliveries
                .AsNoTracking()
                .Include(d => d.Zone)
                .Include(d => d.DeliveryShifts)
                    .ThenInclude(ds => ds.Shift)
                .Where(d => d.CityId == cityId && d.IsActive && !d.IsDeleted)
                .ToListAsync(cancellationToken);

            var riderIds = riders.Select(r => r.DeliveryId).ToList();

            var openLegs = await (
                    from dmo in _context.DeliveryMenOrders.AsNoTracking()
                    join ov in _context.OrderVehicles.AsNoTracking()
                        on new { dmo.OrderId, dmo.VehicleId } equals new { ov.OrderId, ov.VehicleId }
                    where riderIds.Contains(dmo.DeliveryId)
                          && !ov.DeliveryFailed
                          && dmo.Order.OrderState != OrderState.Completed
                          && dmo.Order.OrderState != OrderState.Cancelled
                          && dmo.Order.OrderState != OrderState.CustomerRejectedReceipt
                          && ((dmo.Leg == DeliveryLeg.Delivery && !ov.DeliveredToCustomer)
                              || (dmo.Leg == DeliveryLeg.Return && !ov.DeliveredToOwner))
                    select dmo.DeliveryId)
                .ToListAsync(cancellationToken);

            var openByRider = openLegs.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
            var localNow = RiderTime.LocalNow();

            var result = riders
                .Select(r =>
                {
                    var availability = RiderAvailability.Compute(r.IsOnline, r.DeliveryShifts.Select(ds => ds.Shift), localNow, r.OnlineStatusChangedAt);
                    return new DeliveryAssignCandidateDto
                    {
                        DeliveryId = r.DeliveryId,
                        FullName = r.FullName,
                        MobileNumber = r.MobileNumber,
                        ZoneId = r.ZoneId,
                        ZoneName = r.Zone?.Name ?? string.Empty,
                        Status = availability.Status,
                        IsOnline = availability.IsEffectivelyOnline,
                        IsInShift = availability.IsInShift,
                        CurrentShiftName = availability.CurrentShift?.Name,
                        CurrentShiftEndsAt = availability.CurrentShiftEndsAt.HasValue
                            ? RiderTime.ToUtc(availability.CurrentShiftEndsAt.Value)
                            : null,
                        ActiveLegsCount = openByRider.TryGetValue(r.DeliveryId, out var c) ? c : 0
                    };
                })
                .OrderBy(x => x.Status)
                .ThenBy(x => x.ActiveLegsCount)
                .ThenBy(x => x.FullName)
                .ToList();

            return Result.Success(result);
        }
    }
}
