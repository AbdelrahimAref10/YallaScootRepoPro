using Application.Features.Delivery.Common;
using Application.Features.DeliveryApp.Common;
using Application.Features.Order.Command.OrderVehicleLifecycleCommands;
using CSharpFunctionalExtensions;
using Domain.Common;
using Domain.Enums;
using Domain.Models;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.DeliveryApp.Command.SubmitRiderHandoverCommand
{
    public record RiderHandoverImage
    {
        public HandoverImagePosition Position { get; set; }

        /// <summary>URL returned by the upload endpoint.</summary>
        public string Url { get; set; } = string.Empty;
    }

    /// <summary>
    /// A rider performs one handover step on one vehicle. Checks that the rider holds the leg the step
    /// belongs to, that exactly the four photos are attached, and that cash to collect is confirmed,
    /// then runs the same lifecycle command the admin uses.
    /// </summary>
    public record SubmitRiderHandoverCommand : IRequest<Result<bool>>
    {
        public int OrderId { get; set; }
        public int VehicleId { get; set; }
        public HandoverStep Step { get; set; }
        public List<RiderHandoverImage> Images { get; set; } = new();

        /// <summary>Cash the rider confirms collecting (DeliveredToCustomer on a cash order); otherwise 0.</summary>
        public decimal AmountConfirmed { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>The customer did not take the vehicle; only the rider holding its delivery trip can report it.</summary>
    public record RiderNotReceivedByCustomerCommand : IRequest<Result<bool>>
    {
        public int OrderId { get; set; }
        public int VehicleId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public FaultParty FaultParty { get; set; }
    }

    public class SubmitRiderHandoverCommandHandler :
        IRequestHandler<SubmitRiderHandoverCommand, Result<bool>>,
        IRequestHandler<RiderNotReceivedByCustomerCommand, Result<bool>>
    {
        private const string UploadPrefix = "/uploads/order-vehicles/";

        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;
        private readonly ISender _sender;

        public SubmitRiderHandoverCommandHandler(DatabaseContext context, IUserSession userSession, ISender sender)
        {
            _context = context;
            _userSession = userSession;
            _sender = sender;
        }

        public async Task<Result<bool>> Handle(SubmitRiderHandoverCommand request, CancellationToken cancellationToken)
        {
            if (!Enum.IsDefined(typeof(HandoverStep), request.Step))
                return Result.Failure<bool>("Invalid step");

            var rider = await RiderContext.GetCurrentAsync(_context, _userSession, cancellationToken);
            if (rider.IsFailure)
                return Result.Failure<bool>(rider.Error);
            var riderId = rider.Value.DeliveryId;

            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderVehicles)
                .Include(o => o.DeliveryMenOrders)
                .FirstOrDefaultAsync(o => o.OrderId == request.OrderId, cancellationToken);

            var ov = order?.OrderVehicles.FirstOrDefault(v => v.VehicleId == request.VehicleId);
            var holder = order?.DeliveryMenOrders.FirstOrDefault(d => d.VehicleId == request.VehicleId && d.Leg == request.Step.Leg());

            if (order == null || ov == null || !order.DeliveryMenOrders.Any(d => d.DeliveryId == riderId))
                return Result.Failure<bool>($"Order {request.OrderId} not found");

            if (holder?.DeliveryId != riderId)
                return Result.Failure<bool>("This step is assigned to another rider");

            var images = request.Images ?? new List<RiderHandoverImage>();
            var positions = images.Select(i => i.Position).ToList();
            if (images.Count != 4
                || positions.Distinct().Count() != 4
                || positions.Any(p => !Enum.IsDefined(typeof(HandoverImagePosition), p)))
                return Result.Failure<bool>("4 images are required: front, back, left and right");

            if (images.Any(i => string.IsNullOrWhiteSpace(i.Url) || !i.Url.StartsWith(UploadPrefix, StringComparison.OrdinalIgnoreCase)))
                return Result.Failure<bool>("Upload the images first and send the returned URLs");

            // Retried after a network drop: the step is already recorded, nothing to do.
            if (RiderOrderReader.IsDone(ov, request.Step))
                return Result.Success(true);

            if (request.Step == HandoverStep.DeliveredToCustomer
                && order.PaymentMethodId == (int)PaymentMethod.Cash
                && !order.OrderTotalDebitedToCompany
                && order.OrderTotal > 0
                && request.AmountConfirmed != order.OrderTotal)
                return Result.Failure<bool>($"Confirm collecting {order.OrderTotal:0.##} from the customer");

            var front = images.First(i => i.Position == HandoverImagePosition.Front).Url;

            IRequest<Result<bool>> lifecycle = request.Step switch
            {
                HandoverStep.ReceivedFromOwner => new MarkVehicleReceivedFromOwnerCommand { OrderId = request.OrderId, VehicleId = request.VehicleId, ImageUrl = front },
                HandoverStep.DeliveredToCustomer => new MarkVehicleDeliveredToCustomerCommand { OrderId = request.OrderId, VehicleId = request.VehicleId, ImageUrl = front },
                HandoverStep.ReceivedFromCustomer => new MarkVehicleReceivedFromCustomerCommand { OrderId = request.OrderId, VehicleId = request.VehicleId, ImageUrl = front },
                _ => new MarkVehicleDeliveredToOwnerCommand { OrderId = request.OrderId, VehicleId = request.VehicleId, ImageUrl = front }
            };

            var result = await _sender.Send(lifecycle, cancellationToken);
            if (result.IsFailure)
                return result;

            var stale = await _context.OrderVehicleHandoverImages
                .AsTracking()
                .Where(i => i.OrderId == request.OrderId && i.VehicleId == request.VehicleId && i.Step == request.Step)
                .ToListAsync(cancellationToken);
            _context.OrderVehicleHandoverImages.RemoveRange(stale);

            var actor = _userSession.UserName ?? "System";
            foreach (var image in images)
            {
                _context.OrderVehicleHandoverImages.Add(OrderVehicleHandoverImage.Create(
                    request.OrderId, request.VehicleId, request.Step, image.Position, image.Url, riderId, actor));
            }

            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success(true);
        }

        public async Task<Result<bool>> Handle(RiderNotReceivedByCustomerCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                return Result.Failure<bool>("Reason is required");

            var rider = await RiderContext.GetCurrentAsync(_context, _userSession, cancellationToken);
            if (rider.IsFailure)
                return Result.Failure<bool>(rider.Error);

            var holder = await _context.DeliveryMenOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.OrderId == request.OrderId
                    && d.VehicleId == request.VehicleId
                    && d.Leg == DeliveryLeg.Delivery, cancellationToken);

            if (holder?.DeliveryId != rider.Value.DeliveryId)
                return Result.Failure<bool>("This step is assigned to another rider");

            return await _sender.Send(new MarkVehicleNotReceivedByCustomerCommand
            {
                OrderId = request.OrderId,
                VehicleId = request.VehicleId,
                Reason = request.Reason.Trim(),
                FaultParty = request.FaultParty
            }, cancellationToken);
        }
    }
}
