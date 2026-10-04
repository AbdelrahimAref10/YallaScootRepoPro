using Application.Features.Order.DTOs;
using Domain.Enums;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Order.Common
{
    /// <summary>
    /// Riders on a customer's orders. The phone number is only shared while that rider's trip is under way:
    /// the delivery rider once he has the vehicle and until he hands it over (order on the way), and the
    /// return rider while the customer has the vehicle and is waiting for the pickup.
    /// </summary>
    public static class CustomerOrderRiders
    {
        public static async Task<Dictionary<int, List<CustomerOrderRiderDto>>> LoadAsync(
            DatabaseContext context,
            IReadOnlyCollection<int> orderIds,
            CancellationToken cancellationToken)
        {
            if (orderIds.Count == 0)
                return new Dictionary<int, List<CustomerOrderRiderDto>>();

            var rows = await context.DeliveryMenOrders
                .AsNoTracking()
                .Where(d => orderIds.Contains(d.OrderId))
                .Select(d => new
                {
                    d.OrderId,
                    d.VehicleId,
                    VehicleName = d.Vehicle.Name,
                    d.Vehicle.VehicleCode,
                    d.Leg,
                    RiderName = d.Delivery.FullName,
                    RiderMobile = d.Delivery.MobileNumber,
                    d.Order.OrderState,
                    Link = d.Order.OrderVehicles
                        .Where(ov => ov.VehicleId == d.VehicleId)
                        .Select(ov => new { ov.ReceivedFromOwner, ov.DeliveredToCustomer, ov.ReceivedFromCustomer, ov.DeliveryFailed })
                        .FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(r => r.OrderId)
                .ToDictionary(g => g.Key, g => g
                    .OrderBy(r => r.VehicleId)
                    .ThenBy(r => r.Leg)
                    .Select(r =>
                    {
                        var canCall = r.Link != null && !r.Link.DeliveryFailed && (r.Leg == DeliveryLeg.Delivery
                            ? r.OrderState == OrderState.OnWay && r.Link.ReceivedFromOwner && !r.Link.DeliveredToCustomer
                            : (r.OrderState == OrderState.OnWay || r.OrderState == OrderState.CustomerReceived)
                                && r.Link.DeliveredToCustomer && !r.Link.ReceivedFromCustomer);

                        return new CustomerOrderRiderDto
                        {
                            VehicleId = r.VehicleId,
                            VehicleName = r.VehicleName,
                            VehicleCode = r.VehicleCode,
                            Leg = r.Leg,
                            RiderName = r.RiderName,
                            CanCall = canCall,
                            RiderMobileNumber = canCall ? r.RiderMobile : null
                        };
                    })
                    .ToList());
        }
    }
}
