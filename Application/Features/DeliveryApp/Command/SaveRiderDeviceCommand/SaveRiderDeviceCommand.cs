using Application.Features.Delivery.Common;
using CSharpFunctionalExtensions;
using Domain.Common;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.DeliveryApp.Command.SaveRiderDeviceCommand
{
    /// <summary>Stores the rider's FCM token (Android and/or iOS).</summary>
    public record SaveRiderDeviceCommand : IRequest<Result<bool>>
    {
        public string? AndriodDevice { get; set; }
        public string? IosDevice { get; set; }
    }

    /// <summary>Forgets a token on logout.</summary>
    public record RemoveRiderDeviceCommand : IRequest<Result<bool>>
    {
        public string Token { get; set; } = string.Empty;
    }

    public class SaveRiderDeviceCommandHandler :
        IRequestHandler<SaveRiderDeviceCommand, Result<bool>>,
        IRequestHandler<RemoveRiderDeviceCommand, Result<bool>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;

        public SaveRiderDeviceCommandHandler(DatabaseContext context, IUserSession userSession)
        {
            _context = context;
            _userSession = userSession;
        }

        public async Task<Result<bool>> Handle(SaveRiderDeviceCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.AndriodDevice) && string.IsNullOrWhiteSpace(request.IosDevice))
                return Result.Failure<bool>("A device token is required");

            var rider = await RiderContext.GetCurrentAsync(_context, _userSession, cancellationToken, tracking: true);
            if (rider.IsFailure)
                return Result.Failure<bool>(rider.Error);

            // A phone shared by two riders must only get the pushes of whoever is logged in now.
            var tokens = new[] { request.AndriodDevice, request.IosDevice }
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!)
                .ToList();
            var others = await _context.Deliveries
                .AsTracking()
                .Where(d => d.DeliveryId != rider.Value.DeliveryId
                    && ((d.AndriodDevice != null && tokens.Contains(d.AndriodDevice))
                        || (d.IosDevice != null && tokens.Contains(d.IosDevice))))
                .ToListAsync(cancellationToken);
            foreach (var other in others)
            {
                foreach (var token in tokens)
                    other.ClearDeviceToken(token);
            }

            rider.Value.SaveDeviceTokens(request.AndriodDevice, request.IosDevice);
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success(true);
        }

        public async Task<Result<bool>> Handle(RemoveRiderDeviceCommand request, CancellationToken cancellationToken)
        {
            var rider = await RiderContext.GetCurrentAsync(_context, _userSession, cancellationToken, tracking: true);
            if (rider.IsFailure)
                return Result.Failure<bool>(rider.Error);

            if (!string.IsNullOrWhiteSpace(request.Token))
            {
                rider.Value.ClearDeviceToken(request.Token);
                await _context.SaveChangesAsync(cancellationToken);
            }
            return Result.Success(true);
        }
    }
}
