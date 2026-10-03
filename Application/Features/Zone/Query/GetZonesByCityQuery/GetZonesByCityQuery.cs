using CSharpFunctionalExtensions;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Zone.Query.GetZonesByCityQuery
{
    public record ZoneLookupDto
    {
        public int ZoneId { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        /// <summary>From the caller's location; null when no location was sent.</summary>
        public double? DistanceKm { get; set; }
    }

    /// <summary>
    /// Active zones of the city's zone group. With <see cref="Latitude"/> and <see cref="Longitude"/>
    /// the list is nearest first (the first item is the caller's nearest zone); otherwise by name.
    /// </summary>
    public record GetZonesByCityQuery : IRequest<Result<List<ZoneLookupDto>>>
    {
        public int CityId { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    public class GetZonesByCityQueryHandler : IRequestHandler<GetZonesByCityQuery, Result<List<ZoneLookupDto>>>
    {
        private readonly DatabaseContext _context;

        public GetZonesByCityQueryHandler(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<Result<List<ZoneLookupDto>>> Handle(GetZonesByCityQuery request, CancellationToken cancellationToken)
        {
            if (request.CityId <= 0)
                return Result.Failure<List<ZoneLookupDto>>("City ID is required");

            var city = await _context.Cities.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CityId == request.CityId && c.IsActive, cancellationToken);

            if (city == null)
                return Result.Failure<List<ZoneLookupDto>>("City not found or inactive");

            if (city.ZoneGroupId == null)
                return Result.Success(new List<ZoneLookupDto>());

            var zones = await _context.Zones.AsNoTracking()
                .Where(z => z.ZoneGroupId == city.ZoneGroupId.Value && z.IsActive)
                .OrderBy(z => z.Name)
                .Select(z => new ZoneLookupDto
                {
                    ZoneId = z.ZoneId,
                    Name = z.Name,
                    Latitude = z.Latitude,
                    Longitude = z.Longitude
                })
                .ToListAsync(cancellationToken);

            if (request.Latitude is double lat && request.Longitude is double lng
                && lat is >= -90 and <= 90 && lng is >= -180 and <= 180)
            {
                foreach (var zone in zones)
                    zone.DistanceKm = Math.Round(global::Domain.Models.Zone.DistanceKm(lat, lng, zone.Latitude, zone.Longitude), 2);

                zones = zones.OrderBy(z => z.DistanceKm).ThenBy(z => z.Name).ToList();
            }

            return Result.Success(zones);
        }
    }
}
