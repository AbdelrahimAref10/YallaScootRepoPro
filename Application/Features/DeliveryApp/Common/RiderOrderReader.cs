using Application.Features.DeliveryApp.DTOs;
using Domain.Enums;
using Domain.Models;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.DeliveryApp.Common
{
    /// <summary>
    /// Builds the rider's view of orders. A rider sees an order when he holds at least one leg on it;
    /// steps of the other leg are shown read-only (with photos) so the return rider can compare.
    /// </summary>
    public static class RiderOrderReader
    {
        private static readonly HandoverStep[] Steps =
        {
            HandoverStep.ReceivedFromOwner,
            HandoverStep.DeliveredToCustomer,
            HandoverStep.ReceivedFromCustomer,
            HandoverStep.DeliveredToOwner
        };

        public static bool IsDone(OrderVehicle ov, HandoverStep step) => step switch
        {
            HandoverStep.ReceivedFromOwner => ov.ReceivedFromOwner,
            HandoverStep.DeliveredToCustomer => ov.DeliveredToCustomer,
            HandoverStep.ReceivedFromCustomer => ov.ReceivedFromCustomer,
            _ => ov.DeliveredToOwner
        };

        private static DateTime? DoneAt(OrderVehicle ov, HandoverStep step) => step switch
        {
            HandoverStep.ReceivedFromOwner => ov.ReceivedFromOwnerAt,
            HandoverStep.DeliveredToCustomer => ov.DeliveredToCustomerAt,
            HandoverStep.ReceivedFromCustomer => ov.ReceivedFromCustomerAt,
            _ => ov.DeliveredToOwnerAt
        };

        private static string? LegacyImage(OrderVehicle ov, HandoverStep step) => step switch
        {
            HandoverStep.ReceivedFromOwner => ov.ReceivedFromOwnerImageUrl,
            HandoverStep.DeliveredToCustomer => ov.DeliveredToCustomerImageUrl,
            HandoverStep.ReceivedFromCustomer => ov.ReceivedFromCustomerImageUrl,
            _ => ov.DeliveredToOwnerImageUrl
        };

        /// <summary>First step nobody has done yet; null when the vehicle cycle is finished or cancelled.</summary>
        public static HandoverStep? PendingStep(OrderVehicle ov)
        {
            if (ov.DeliveryFailed)
                return null;
            foreach (var step in Steps)
            {
                if (!IsDone(ov, step))
                    return step;
            }
            return null;
        }

        /// <summary>The step this rider can do now on this vehicle, if any.</summary>
        public static HandoverStep? MyNextStep(Domain.Models.Order order, OrderVehicle ov, IEnumerable<DeliveryMenOrder> assignments, int riderId)
        {
            if (order.OrderState is OrderState.Cancelled or OrderState.Completed or OrderState.CustomerRejectedReceipt)
                return null;

            var pending = PendingStep(ov);
            if (pending == null)
                return null;

            // Pickup is only allowed once every vehicle has a delivery rider (see Order.EnsureOperationalStateForPickup).
            if (pending == HandoverStep.ReceivedFromOwner
                && order.OrderState is not (OrderState.DeliveryAssigned or OrderState.OnWay))
                return null;

            var holder = assignments.FirstOrDefault(a => a.VehicleId == ov.VehicleId && a.Leg == pending.Value.Leg());
            return holder?.DeliveryId == riderId ? pending : null;
        }

        /// <summary>Is the rider's part of the order still open (and on which leg)?</summary>
        public static RiderOrderTab TabFor(Domain.Models.Order order, IReadOnlyList<DeliveryMenOrder> mine)
        {
            if (order.OrderState is OrderState.Cancelled or OrderState.Completed or OrderState.CustomerRejectedReceipt)
                return RiderOrderTab.Completed;

            bool LegOpen(DeliveryMenOrder a)
            {
                var ov = order.OrderVehicles.FirstOrDefault(v => v.VehicleId == a.VehicleId);
                if (ov == null || ov.DeliveryFailed)
                    return false;
                return a.Leg == DeliveryLeg.Delivery ? !ov.DeliveredToCustomer : !ov.DeliveredToOwner;
            }

            if (mine.Any(a => a.Leg == DeliveryLeg.Delivery && LegOpen(a)))
                return RiderOrderTab.Pickup;
            if (mine.Any(a => a.Leg == DeliveryLeg.Return && LegOpen(a)))
                return RiderOrderTab.Return;
            return RiderOrderTab.Completed;
        }

        /// <summary>Loads orders where the rider holds a leg, with everything the rider view needs.</summary>
        public static IQueryable<Domain.Models.Order> MyOrders(DatabaseContext context, int riderId) =>
            context.Orders
                .AsNoTracking()
                .AsSplitQuery()
                .Include(o => o.Customer)
                .Include(o => o.DestinationZone)
                .Include(o => o.OrderVehicles)
                    .ThenInclude(ov => ov.Vehicle)
                        .ThenInclude(v => v.Merchant)
                            .ThenInclude(m => m.Zone)
                .Include(o => o.DeliveryMenOrders)
                    .ThenInclude(d => d.Delivery)
                .Include(o => o.DeliveryOrderPaymentDetails)
                .Where(o => o.DeliveryMenOrders.Any(d => d.DeliveryId == riderId));

        public static decimal MyCollect(Domain.Models.Order order, int riderId, IReadOnlyList<OrderJournal> cashJournals)
        {
            if (order.PaymentMethodId != (int)PaymentMethod.Cash)
                return 0m;

            if (order.OrderTotalDebitedToCompany)
            {
                return cashJournals
                    .Where(j => j.OrderId == order.OrderId
                        && j.PartyId == riderId
                        && j.EntryKind == OrderJournalEntryKind.CashCollectedFromCustomer)
                    .Sum(j => j.Amount);
            }

            // Not collected yet: whoever delivers the first scooter collects. Show it to every delivery rider still delivering.
            var deliveringNow = order.DeliveryMenOrders.Any(d =>
                d.DeliveryId == riderId
                && d.Leg == DeliveryLeg.Delivery
                && order.OrderVehicles.Any(ov => ov.VehicleId == d.VehicleId && !ov.DeliveryFailed && !ov.DeliveredToCustomer));
            return deliveringNow ? order.OrderTotal : 0m;
        }

        public static decimal MyCommission(Domain.Models.Order order, int riderId) =>
            order.DeliveryOrderPaymentDetails.Where(p => p.DeliveryId == riderId).Sum(p => p.DeliveryFeeShare);

        public static RiderOrderSummaryDto ToSummary(Domain.Models.Order order, int riderId, IReadOnlyList<OrderJournal> cashJournals)
        {
            var mine = order.DeliveryMenOrders.Where(d => d.DeliveryId == riderId).ToList();
            var next = order.OrderVehicles
                .Select(ov => MyNextStep(order, ov, order.DeliveryMenOrders, riderId))
                .Where(s => s.HasValue)
                .Select(s => s!.Value)
                .DefaultIfEmpty()
                .Min();

            return new RiderOrderSummaryDto
            {
                OrderId = order.OrderId,
                OrderCode = order.OrderCode,
                OrderState = order.OrderState,
                IsUrgent = order.IsUrgent,
                CustomerName = order.Customer?.FullName ?? string.Empty,
                HotelName = order.HotelName,
                DestinationZoneName = order.DestinationZone?.Name ?? string.Empty,
                ReservationDateFrom = order.ReservationDateFrom,
                ReservationDateTo = order.ReservationDateTo,
                MyVehiclesCount = mine.Select(m => m.VehicleId).Distinct().Count(),
                NextAction = next == default ? RiderActions.None : RiderActions.For(next),
                CollectFromCustomer = MyCollect(order, riderId, cashJournals),
                IsPaidOnline = order.PaymentMethodId != (int)PaymentMethod.Cash,
                MyDeliveryFeeShare = MyCommission(order, riderId)
            };
        }

        public static RiderOrderDetailDto ToDetail(
            Domain.Models.Order order,
            OrderTotals? totals,
            int riderId,
            IReadOnlyList<OrderJournal> cashJournals,
            IReadOnlyList<OrderVehicleHandoverImage> images)
        {
            var myVehicleIds = order.DeliveryMenOrders.Where(d => d.DeliveryId == riderId).Select(d => d.VehicleId).ToHashSet();

            var vehicles = order.OrderVehicles
                .Where(ov => myVehicleIds.Contains(ov.VehicleId))
                .OrderBy(ov => ov.VehicleId)
                .Select(ov =>
                {
                    RiderLegDto? Leg(DeliveryLeg leg)
                    {
                        var a = order.DeliveryMenOrders.FirstOrDefault(d => d.VehicleId == ov.VehicleId && d.Leg == leg);
                        return a == null ? null : new RiderLegDto
                        {
                            DeliveryId = a.DeliveryId,
                            DeliveryName = a.Delivery?.FullName ?? string.Empty,
                            IsMine = a.DeliveryId == riderId
                        };
                    }

                    var legs = new RiderLegsDto { Delivery = Leg(DeliveryLeg.Delivery), Return = Leg(DeliveryLeg.Return) };

                    RiderStepDto Step(HandoverStep step)
                    {
                        var done = IsDone(ov, step);
                        var stepImages = images
                            .Where(i => i.VehicleId == ov.VehicleId && i.Step == step)
                            .OrderBy(i => i.Position)
                            .Select(i => i.ImageUrl)
                            .ToList();
                        var legacy = LegacyImage(ov, step);
                        if (stepImages.Count == 0 && !string.IsNullOrWhiteSpace(legacy) && !legacy.StartsWith("data:"))
                            stepImages.Add(legacy);
                        var holder = step.Leg() == DeliveryLeg.Delivery ? legs.Delivery : legs.Return;
                        return new RiderStepDto
                        {
                            Done = done,
                            At = DoneAt(ov, step),
                            Images = stepImages,
                            ByDeliveryName = done ? holder?.DeliveryName : null
                        };
                    }

                    var next = MyNextStep(order, ov, order.DeliveryMenOrders, riderId);
                    var money = new RiderStepMoneyDto();
                    if (next == HandoverStep.DeliveredToCustomer
                        && order.PaymentMethodId == (int)PaymentMethod.Cash
                        && !order.OrderTotalDebitedToCompany
                        && order.OrderTotal > 0)
                    {
                        money = new RiderStepMoneyDto { Type = "CollectFromCustomer", Amount = order.OrderTotal };
                    }

                    var merchant = ov.Vehicle?.Merchant;
                    return new RiderVehicleDto
                    {
                        VehicleId = ov.VehicleId,
                        VehicleCode = ov.Vehicle?.VehicleCode ?? string.Empty,
                        VehicleName = ov.Vehicle?.Name ?? string.Empty,
                        Model = ov.Vehicle?.Model ?? string.Empty,
                        Color = ov.Vehicle?.Color ?? string.Empty,
                        ImageUrl = ov.Vehicle?.ImageUrl,
                        Merchant = new RiderMerchantDto
                        {
                            MerchantId = merchant?.MerchantId ?? 0,
                            MerchantName = merchant?.FullName ?? string.Empty,
                            MobileNumber = merchant?.MobileNumber ?? string.Empty,
                            ZoneName = merchant?.Zone?.Name ?? string.Empty,
                            Address = merchant?.Zone?.Name ?? string.Empty
                        },
                        Legs = legs,
                        Steps = new RiderStepsDto
                        {
                            ReceivedFromOwner = Step(HandoverStep.ReceivedFromOwner),
                            DeliveredToCustomer = Step(HandoverStep.DeliveredToCustomer),
                            ReceivedFromCustomer = Step(HandoverStep.ReceivedFromCustomer),
                            DeliveredToOwner = Step(HandoverStep.DeliveredToOwner)
                        },
                        NextAction = RiderActions.For(next),
                        NextStepMoney = money,
                        DeliveryFailed = ov.DeliveryFailed,
                        DeliveryFailureReason = ov.DeliveryFailureReason
                    };
                })
                .ToList();

            return new RiderOrderDetailDto
            {
                OrderId = order.OrderId,
                OrderCode = order.OrderCode,
                OrderState = order.OrderState,
                IsUrgent = order.IsUrgent,
                Notes = order.Notes,
                Customer = new RiderCustomerDto
                {
                    Name = order.Customer?.FullName ?? string.Empty,
                    MobileNumber = order.Customer?.MobileNumber ?? string.Empty,
                    HotelName = order.HotelName,
                    HotelAddress = order.HotelAddress,
                    HotelPhone = order.HotelPhone,
                    DestinationZoneName = order.DestinationZone?.Name ?? string.Empty
                },
                ReservationDateFrom = order.ReservationDateFrom,
                ReservationDateTo = order.ReservationDateTo,
                Vehicles = vehicles,
                Money = new RiderOrderMoneyDto
                {
                    PaymentMethod = order.PaymentMethodId,
                    IsPaidOnline = order.PaymentMethodId != (int)PaymentMethod.Cash,
                    CollectFromCustomer = MyCollect(order, riderId, cashJournals),
                    CollectFromCustomerBreakdown = new RiderCollectBreakdownDto
                    {
                        Rental = totals?.SubTotal ?? order.OrderSubTotal,
                        DeliveryFees = totals?.DeliveryFees ?? 0,
                        ServiceFees = totals?.ServiceFees ?? 0,
                        UrgentFees = totals?.UrgentFees ?? 0,
                        TieredDiscount = totals?.TieredDiscount ?? 0,
                        PreviousDebt = order.PreviousDebt
                    },
                    MyDeliveryFeeShare = MyCommission(order, riderId)
                }
            };
        }
    }
}
