using Application.Features.Order.Services;
using CSharpFunctionalExtensions;
using Domain.Common;
using Domain.Enums;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Order.Command.ChangeOrderPaymentToCashCommand
{
    /// <summary>
    /// Admin switches an online order whose payment is still Pending or Failed to cash on delivery.
    /// </summary>
    public record ChangeOrderPaymentToCashCommand : IRequest<Result<bool>>
    {
        public int OrderId { get; set; }
    }

    public class ChangeOrderPaymentToCashCommandHandler : IRequestHandler<ChangeOrderPaymentToCashCommand, Result<bool>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;
        private readonly IOrderRealtimeNotifier _realtime;

        public ChangeOrderPaymentToCashCommandHandler(
            DatabaseContext context,
            IUserSession userSession,
            IOrderRealtimeNotifier realtime)
        {
            _context = context;
            _userSession = userSession;
            _realtime = realtime;
        }

        public async Task<Result<bool>> Handle(ChangeOrderPaymentToCashCommand request, CancellationToken cancellationToken)
        {
            // Tracked: the order and its payment row are modified below.
            var order = await _context.Orders
                .AsTracking()
                .Include(o => o.OrderPayments)
                .Include(o => o.OrderVehicles)
                .FirstOrDefaultAsync(o => o.OrderId == request.OrderId, cancellationToken);

            if (order == null)
                return Result.Failure<bool>($"Order with ID {request.OrderId} not found");

            try
            {
                order.ChangePaymentToCash(_userSession.UserName ?? "Admin");
            }
            catch (InvalidOperationException ex)
            {
                return Result.Failure<bool>(ex.Message);
            }

            await _context.SaveChangesAsync(cancellationToken);

            await _realtime.NotifyAsync(
                order.OrderId,
                "Payment changed to cash",
                $"Order #{order.OrderCode} will be paid in cash on delivery.",
                NotificationType.OrderUpdated,
                cancellationToken: cancellationToken);

            return Result.Success(true);
        }
    }
}
