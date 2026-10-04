using Application.Features.Shifts.Common;
using Application.Features.Shifts.DTOs;
using CSharpFunctionalExtensions;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Shifts.Query.GetShiftsQuery
{
    public record GetShiftsQuery : IRequest<Result<List<ShiftDto>>>
    {
        public int? CityId { get; set; }
        public int? ShiftId { get; set; }
    }

    public class GetShiftsQueryHandler : IRequestHandler<GetShiftsQuery, Result<List<ShiftDto>>>
    {
        private readonly DatabaseContext _context;

        public GetShiftsQueryHandler(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<Result<List<ShiftDto>>> Handle(GetShiftsQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Shifts
                .AsNoTracking()
                .Include(s => s.City)
                .Include(s => s.DeliveryShifts)
                    .ThenInclude(ds => ds.Delivery)
                .Where(s => !s.IsDeleted);

            if (request.CityId.HasValue)
                query = query.Where(s => s.CityId == request.CityId.Value);
            if (request.ShiftId.HasValue)
                query = query.Where(s => s.ShiftId == request.ShiftId.Value);

            var shifts = await query
                .OrderBy(s => s.CityId)
                .ThenBy(s => s.StartTime)
                .ToListAsync(cancellationToken);

            return Result.Success(shifts
                .Select(s => ShiftMapper.ToDto(
                    s,
                    s.City.Name,
                    s.DeliveryShifts.Select(ds => ds.Delivery).Where(d => !d.IsDeleted)))
                .ToList());
        }
    }
}
