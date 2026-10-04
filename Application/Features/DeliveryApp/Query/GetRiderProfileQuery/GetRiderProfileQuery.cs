using Application.Features.Delivery.Common;
using Application.Features.DeliveryApp.DTOs;
using CSharpFunctionalExtensions;
using Domain.Common;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.DeliveryApp.Query.GetRiderProfileQuery
{
    public record GetRiderProfileQuery : IRequest<Result<RiderProfileDto>>;

    public class GetRiderProfileQueryHandler : IRequestHandler<GetRiderProfileQuery, Result<RiderProfileDto>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;

        public GetRiderProfileQueryHandler(DatabaseContext context, IUserSession userSession)
        {
            _context = context;
            _userSession = userSession;
        }

        public async Task<Result<RiderProfileDto>> Handle(GetRiderProfileQuery request, CancellationToken cancellationToken)
        {
            var rider = await RiderContext.GetCurrentAsync(_context, _userSession, cancellationToken);
            if (rider.IsFailure)
                return Result.Failure<RiderProfileDto>(rider.Error);

            var dto = await _context.Deliveries
                .AsNoTracking()
                .Where(d => d.DeliveryId == rider.Value.DeliveryId)
                .Select(d => new RiderProfileDto
                {
                    DeliveryId = d.DeliveryId,
                    FullName = d.FullName,
                    UserName = d.User.UserName ?? string.Empty,
                    MobileNumber = d.MobileNumber,
                    Email = d.Email,
                    PersonalImage = d.PersonalImage,
                    CityId = d.CityId,
                    CityName = d.City.Name,
                    ZoneId = d.ZoneId,
                    ZoneName = d.Zone.Name
                })
                .FirstAsync(cancellationToken);

            return Result.Success(dto);
        }
    }
}
