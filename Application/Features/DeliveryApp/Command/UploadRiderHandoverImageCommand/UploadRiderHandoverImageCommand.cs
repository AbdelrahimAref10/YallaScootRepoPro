using Application.Features.Delivery.Common;
using CSharpFunctionalExtensions;
using Domain.Common;
using Infrastructure;
using Infrastructure.Services;
using MediatR;

namespace Application.Features.DeliveryApp.Command.UploadRiderHandoverImageCommand
{
    /// <summary>Saves one handover photo and returns its relative URL (e.g. /uploads/order-vehicles/x.jpg).</summary>
    public record UploadRiderHandoverImageCommand : IRequest<Result<string>>
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
    }

    public class UploadRiderHandoverImageCommandHandler : IRequestHandler<UploadRiderHandoverImageCommand, Result<string>>
    {
        public const int MaxBytes = 5 * 1024 * 1024;

        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;
        private readonly IImageService _imageService;

        public UploadRiderHandoverImageCommandHandler(DatabaseContext context, IUserSession userSession, IImageService imageService)
        {
            _context = context;
            _userSession = userSession;
            _imageService = imageService;
        }

        public async Task<Result<string>> Handle(UploadRiderHandoverImageCommand request, CancellationToken cancellationToken)
        {
            var rider = await RiderContext.GetCurrentAsync(_context, _userSession, cancellationToken);
            if (rider.IsFailure)
                return Result.Failure<string>(rider.Error);

            if (request.Content == null || request.Content.Length == 0)
                return Result.Failure<string>("Image is required");
            if (request.Content.Length > MaxBytes)
                return Result.Failure<string>("Image must be 5 MB or smaller");

            try
            {
                var url = _imageService.SaveBase64Image(Convert.ToBase64String(request.Content), "order-vehicles");
                return Result.Success(url);
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
            {
                return Result.Failure<string>(ex.Message);
            }
        }
    }
}
