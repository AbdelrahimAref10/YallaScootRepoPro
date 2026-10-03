using Application.Features.Order.Common;
using Application.Features.Order.Services;
using CSharpFunctionalExtensions;
using Domain.Common;
using Domain.Enums;
using Domain.Models;
using Infrastructure;
using Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Order.Command.RejectOrderCommand
{
    /// <summary>
    /// Admin-only order rejection: frees vehicles/reservations with no cancellation fee.
    /// Customer mobile cancel continues to use <see cref="CancelOrderCommand.CancelOrderCommand"/>.
    /// </summary>
    public record RejectOrderCommand : IRequest<Result<bool>>
    {
        public int OrderId { get; set; }
    }

    public class RejectOrderCommandHandler : IRequestHandler<RejectOrderCommand, Result<bool>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;
        private readonly INotificationService _notificationService;
        private readonly IOrderRealtimeNotifier _realtime;

        public RejectOrderCommandHandler(
            DatabaseContext context,
            IUserSession userSession,
            INotificationService notificationService,
            IOrderRealtimeNotifier realtime)
        {
            _context = context;
            _userSession = userSession;
            _notificationService = notificationService;
            _realtime = realtime;
        }

        public async Task<Result<bool>> Handle(RejectOrderCommand request, CancellationToken cancellationToken)
        {
            var order = await _context.Orders
                .AsTracking()
                .Include(o => o.Customer)
                .Include(o => o.OrderPayments)
                .Include(o => o.OrderVehicles)
                    .ThenInclude(ov => ov.Vehicle)
                .FirstOrDefaultAsync(o => o.OrderId == request.OrderId, cancellationToken);

            if (order == null)
            {
                return Result.Failure<bool>($"Order with ID {request.OrderId} not found");
            }

            if (order.OrderState == OrderState.Cancelled
                || await CancellationDebtHelper.IsOrderCancelledAsync(_context, order, cancellationToken))
            {
                return Result.Failure<bool>("Order is already cancelled");
            }

            if (!order.CanCancel())
            {
                return Result.Failure<bool>(
                    "Cannot reject the order after a vehicle has been received from the merchant.");
            }

            if (!_userSession.Roles.Contains(AppRoleNames.SuperAdmin))
            {
                return Result.Failure<bool>("Only admin can reject an order without a cancellation fee");
            }

            // Prior debt attached but not yet paid → release back to Pending
            await CancellationDebtHelper.RevertUnderPaymentFeesToPendingAsync(_context, order.OrderId, cancellationToken);

            var orderPayment = order.OrderPayments.FirstOrDefault();
            var isPayPal = orderPayment != null && orderPayment.PaymentMethodId == (int)PaymentMethod.PayPal;
            // Only a captured (Paid) PayPal payment has money to give back.
            var payPalCaptured = isPayPal && orderPayment!.State == PaymentState.Paid;

            if (payPalCaptured)
            {
                var refundableAmount = order.OrderTotal - order.PreviousDebt;
                if (refundableAmount > 0)
                {
                    var refundablePaypal = RefundablePaypalAmount.Create(
                        order.CustomerId,
                        order.OrderId,
                        order.OrderTotal,
                        cancellationFees: 0,
                        _userSession.UserName ?? "System",
                        order.PreviousDebt);

                    _context.RefundablePaypalAmounts.Add(refundablePaypal);
                }

                orderPayment!.MarkAsRefunded(_userSession.UserName ?? "System");
            }
            else if (isPayPal && orderPayment!.State == PaymentState.Pending)
            {
                // Never captured: close the payment so it cannot be captured on a cancelled order.
                orderPayment.MarkAsFailed(_userSession.UserName ?? "System");
            }

            if (order.OrderState == OrderState.Confirmed
                || order.OrderState == OrderState.OnWay
                || order.OrderState == OrderState.CustomerReceived)
            {
                foreach (var orderVehicle in order.OrderVehicles)
                {
                    orderVehicle.Vehicle.UpdateStatus(VehicleStatus.Available, _userSession.UserName ?? "System");
                }
            }

            var reservedVehicles = await _context.ReservedVehiclesPerDays
                .AsTracking()
                .Where(rv => rv.OrderId == order.OrderId)
                .ToListAsync(cancellationToken);

            foreach (var reserved in reservedVehicles)
            {
                reserved.Cancel(_userSession.UserName ?? "System");
            }

            // Cash → MoneyRefunded=true; PayPal → false until admin confirms
            order.Cancel(_userSession.UserName ?? "System");

            // Unpaid PayPal: nothing was collected, so nothing is owed back (same as cash).
            if (isPayPal && !payPalCaptured)
                order.MarkMoneyRefunded(_userSession.UserName ?? "System");

            await _context.SaveChangesAsync(cancellationToken);

            await _realtime.NotifyAsync(
                order.OrderId,
                "Order Rejected",
                $"Order #{order.OrderCode} was rejected by admin (no cancellation fee)",
                NotificationType.OrderCancelled,
                cancellationToken: cancellationToken);

            return Result.Success(true);
        }

    }
}
