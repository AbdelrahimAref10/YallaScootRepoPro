using Application.Features.Customer.Common;
using Application.Features.Delivery.Common;
using Application.Features.Order.Services;
using CSharpFunctionalExtensions;
using Domain.Common;
using Domain.Enums;
using Domain.Models;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Order.Command.AssignDeliveryToOrderCommand
{
    public record AssignDeliveryVehicleItem
    {
        public int VehicleId { get; set; }
        public int DeliveryId { get; set; }

        /// <summary>
        /// Which trip this rider takes. Null keeps the old behaviour: the same rider gets both legs.
        /// </summary>
        public DeliveryLeg? Leg { get; set; }
    }

    /// <summary>
    /// Assigns or reassigns riders per vehicle and per leg. A leg can be reassigned until it starts:
    /// the delivery leg until the vehicle is received from the owner, the return leg until it is
    /// received from the customer. Rider commission is snapshotted from the city's leg percent.
    /// </summary>
    public record AssignDeliveryToOrderCommand : IRequest<Result<bool>>
    {
        public int OrderId { get; set; }
        public List<AssignDeliveryVehicleItem> Assignments { get; set; } = new();
    }

    public class AssignDeliveryToOrderCommandHandler : IRequestHandler<AssignDeliveryToOrderCommand, Result<bool>>
    {
        private static readonly OrderState[] DeliveryLegStates =
            { OrderState.Confirmed, OrderState.DeliveryAssigned, OrderState.OnWay };

        private static readonly OrderState[] ReturnLegStates =
            { OrderState.Confirmed, OrderState.DeliveryAssigned, OrderState.OnWay, OrderState.CustomerReceived };

        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;
        private readonly IOrderRealtimeNotifier _realtime;
        private readonly IRiderNotifier _riderNotifier;
        private readonly ICustomerNotifier _customerNotifier;

        public AssignDeliveryToOrderCommandHandler(
            DatabaseContext context,
            IUserSession userSession,
            IOrderRealtimeNotifier realtime,
            IRiderNotifier riderNotifier,
            ICustomerNotifier customerNotifier)
        {
            _context = context;
            _userSession = userSession;
            _realtime = realtime;
            _riderNotifier = riderNotifier;
            _customerNotifier = customerNotifier;
        }

        public async Task<Result<bool>> Handle(AssignDeliveryToOrderCommand request, CancellationToken cancellationToken)
        {
            if (request.Assignments == null || request.Assignments.Count == 0)
                return Result.Failure<bool>("At least one vehicle/delivery assignment is required");

            // Expand "no leg" into both legs.
            var items = new List<(int VehicleId, int DeliveryId, DeliveryLeg Leg)>();
            foreach (var a in request.Assignments)
            {
                if (a.Leg.HasValue)
                {
                    items.Add((a.VehicleId, a.DeliveryId, a.Leg.Value));
                }
                else
                {
                    items.Add((a.VehicleId, a.DeliveryId, DeliveryLeg.Delivery));
                    items.Add((a.VehicleId, a.DeliveryId, DeliveryLeg.Return));
                }
            }

            if (items.Any(i => !Enum.IsDefined(typeof(DeliveryLeg), i.Leg)))
                return Result.Failure<bool>("Invalid leg");

            if (items.Select(i => (i.VehicleId, i.Leg)).Distinct().Count() != items.Count)
                return Result.Failure<bool>("Duplicate vehicle/leg assignments are not allowed");

            var order = await _context.Orders
                .AsTracking()
                .Include(o => o.OrderVehicles)
                .FirstOrDefaultAsync(o => o.OrderId == request.OrderId, cancellationToken);

            if (order == null)
                return Result.Failure<bool>($"Order with ID {request.OrderId} not found");

            foreach (var item in items)
            {
                var ov = order.OrderVehicles.FirstOrDefault(v => v.VehicleId == item.VehicleId);
                if (ov == null)
                    return Result.Failure<bool>("One or more vehicles are not assigned to this order");
                if (ov.DeliveryFailed)
                    return Result.Failure<bool>($"Vehicle {item.VehicleId} was cancelled on this order");
            }

            var vehicleIds = items.Select(i => i.VehicleId).Distinct().ToList();

            var existingAssignments = await _context.DeliveryMenOrders
                .AsTracking()
                .Where(d => d.OrderId == request.OrderId && vehicleIds.Contains(d.VehicleId))
                .ToListAsync(cancellationToken);

            var existingPaymentDetails = await _context.DeliveryOrderPaymentDetails
                .AsTracking()
                .Where(d => d.OrderId == request.OrderId && vehicleIds.Contains(d.VehicleId))
                .ToListAsync(cancellationToken);

            // Only rows that actually change are validated and written. The old admin sends no leg (= both legs),
            // so an unchanged leg must not make the whole request fail on state or "already started" checks.
            items = items
                .Where(i => !existingAssignments.Any(e => e.VehicleId == i.VehicleId && e.Leg == i.Leg && e.DeliveryId == i.DeliveryId))
                .ToList();

            if (items.Count == 0)
                return Result.Success(true);

            if (items.Any(i => i.Leg == DeliveryLeg.Delivery) && !DeliveryLegStates.Contains(order.OrderState))
                return Result.Failure<bool>($"Cannot assign the delivery trip in {order.OrderState} state. Order must be Confirmed");

            if (items.Any(i => i.Leg == DeliveryLeg.Return) && !ReturnLegStates.Contains(order.OrderState))
                return Result.Failure<bool>($"Cannot assign the return trip in {order.OrderState} state");

            var deliveryIds = items.Select(i => i.DeliveryId).Distinct().ToList();
            var deliveries = await _context.Deliveries
                .AsNoTracking()
                .Where(d => deliveryIds.Contains(d.DeliveryId) && d.IsActive && !d.IsDeleted)
                .Select(d => new { d.DeliveryId, d.CityId })
                .ToListAsync(cancellationToken);

            if (deliveries.Count != deliveryIds.Count)
                return Result.Failure<bool>("One or more deliveries were not found or are inactive");

            if (deliveries.Any(d => d.CityId != order.CityId))
                return Result.Failure<bool>("All deliveries must belong to the same city as the order");

            var city = await _context.Cities
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CityId == order.CityId, cancellationToken);
            if (city == null)
                return Result.Failure<bool>("Order city not found");

            var createdBy = _userSession.UserName ?? "System";

            var assigned = new List<(int DeliveryId, DeliveryLeg Leg, int VehicleId)>();
            var unassigned = new List<(int DeliveryId, DeliveryLeg Leg, int VehicleId)>();

            foreach (var item in items)
            {
                var ov = order.OrderVehicles.First(v => v.VehicleId == item.VehicleId);
                var started = item.Leg == DeliveryLeg.Delivery ? ov.ReceivedFromOwner : ov.ReceivedFromCustomer;

                var existing = existingAssignments.FirstOrDefault(d => d.VehicleId == item.VehicleId && d.Leg == item.Leg);

                if (started)
                {
                    return Result.Failure<bool>(item.Leg == DeliveryLeg.Delivery
                        ? $"Vehicle {item.VehicleId} was already received from the owner; its delivery rider cannot change"
                        : $"Vehicle {item.VehicleId} was already received from the customer; its return rider cannot change");
                }

                if (existing != null)
                {
                    unassigned.Add((existing.DeliveryId, item.Leg, item.VehicleId));
                    _context.DeliveryMenOrders.Remove(existing);
                }

                await _context.DeliveryMenOrders.AddAsync(
                    DeliveryMenOrder.Create(request.OrderId, item.VehicleId, item.DeliveryId, createdBy, item.Leg),
                    cancellationToken);

                var paymentDetail = existingPaymentDetails.FirstOrDefault(d => d.VehicleId == item.VehicleId && d.Leg == item.Leg);

                // A reassigned leg keeps the percent it was first assigned with (orders from before
                // the per-leg split carry 100/0), so the order never pays more or less than it did.
                var percent = paymentDetail?.CommissionPercent ?? city.CommissionPercentFor(item.Leg);
                var share = DeliveryOrderPaymentDetail.ComputeShare(ov.DeliveryFee, percent);
                if (paymentDetail != null)
                    _context.DeliveryOrderPaymentDetails.Remove(paymentDetail);

                await _context.DeliveryOrderPaymentDetails.AddAsync(
                    DeliveryOrderPaymentDetail.Create(
                        request.OrderId,
                        item.DeliveryId,
                        item.VehicleId,
                        share,
                        createdBy,
                        item.Leg,
                        percent),
                    cancellationToken);

                assigned.Add((item.DeliveryId, item.Leg, item.VehicleId));
            }

            if (assigned.Count == 0)
                return Result.Success(true);

            // The order becomes DeliveryAssigned once every active vehicle has a delivery-trip rider.
            var otherDeliveryLegVehicleIds = await _context.DeliveryMenOrders
                .AsNoTracking()
                .Where(d => d.OrderId == request.OrderId && d.Leg == DeliveryLeg.Delivery && !vehicleIds.Contains(d.VehicleId))
                .Select(d => d.VehicleId)
                .ToListAsync(cancellationToken);

            var covered = otherDeliveryLegVehicleIds
                .Concat(items.Where(i => i.Leg == DeliveryLeg.Delivery).Select(i => i.VehicleId))
                .Concat(existingAssignments.Where(e => e.Leg == DeliveryLeg.Delivery).Select(e => e.VehicleId))
                .ToHashSet();

            var required = order.OrderVehicles
                .Where(ov => ov.MerchantResponseStatus != MerchantVehicleResponseStatus.Declined && !ov.DeliveryFailed)
                .Select(ov => ov.VehicleId)
                .ToHashSet();

            if (order.OrderState == OrderState.Confirmed
                && required.Count > 0
                && required.All(id => covered.Contains(id)))
                order.MarkDeliveryAssigned(createdBy);

            await _context.SaveChangesAsync(cancellationToken);

            await _realtime.NotifyAsync(
                order.OrderId,
                "Delivery assigned",
                $"Delivery was assigned on order #{order.OrderCode}.",
                NotificationType.OrderUpdated,
                cancellationToken: cancellationToken);

            foreach (var group in assigned.GroupBy(a => (a.DeliveryId, a.Leg)))
            {
                var count = group.Select(g => g.VehicleId).Distinct().Count();
                var trip = group.Key.Leg == DeliveryLeg.Delivery ? "توصيل" : "استرجاع";
                await _riderNotifier.NotifyAsync(
                    group.Key.DeliveryId,
                    $"اتعيّن لك {trip} — أوردر {order.OrderCode}",
                    count == 1 ? $"سكوتر واحد ({trip})." : $"{count} سكوتر ({trip}).",
                    group.Key.Leg == DeliveryLeg.Delivery ? "DeliveryLegAssigned" : "ReturnLegAssigned",
                    order.OrderId,
                    order.OrderCode,
                    cancellationToken);
            }

            foreach (var riderId in unassigned.Select(u => u.DeliveryId).Distinct())
            {
                await _riderNotifier.NotifyAsync(
                    riderId,
                    $"اتشال منك مشوار — أوردر {order.OrderCode}",
                    "الأدمن نقل المشوار ده لطيار تاني.",
                    "LegUnassigned",
                    order.OrderId,
                    order.OrderCode,
                    cancellationToken);
            }

            await NotifyCustomerAsync(order.OrderId, assigned, cancellationToken);

            return Result.Success(true);
        }

        /// <summary>One push per trip type: who brings the scooter, and who picks it up at the end.</summary>
        private async Task NotifyCustomerAsync(
            int orderId,
            List<(int DeliveryId, DeliveryLeg Leg, int VehicleId)> assigned,
            CancellationToken cancellationToken)
        {
            var riderIds = assigned.Select(a => a.DeliveryId).Distinct().ToList();
            var names = await _context.Deliveries
                .AsNoTracking()
                .Where(d => riderIds.Contains(d.DeliveryId))
                .ToDictionaryAsync(d => d.DeliveryId, d => d.FullName, cancellationToken);

            foreach (var leg in assigned.GroupBy(a => a.Leg))
            {
                var riders = string.Join(" و", leg
                    .Select(a => names.TryGetValue(a.DeliveryId, out var n) ? n : null)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct());

                var (title, body) = leg.Key == DeliveryLeg.Delivery
                    ? ("تم تعيين مندوب التوصيل",
                        string.IsNullOrEmpty(riders)
                            ? "اتعيّن مندوب يوصّلك طلبك #{code}."
                            : $"{riders} هيوصّلك طلبك #{{code}}.")
                    : ("تم تعيين مندوب الاستلام",
                        string.IsNullOrEmpty(riders)
                            ? "اتعيّن مندوب ياخد المركبة منك في نهاية حجز طلبك #{code}."
                            : $"{riders} هياخد المركبة منك في نهاية حجز طلبك #{{code}}.");

                await _customerNotifier.NotifyOrderAsync(orderId, title, body, NotificationType.RiderAssigned, cancellationToken);
            }
        }
    }
}
