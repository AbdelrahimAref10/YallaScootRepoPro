using Application.Features.Shifts.Common;
using CSharpFunctionalExtensions;
using Domain.Common;
using Domain.Models;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Shifts.Command.SaveShiftCommand
{
    /// <summary>Creates (ShiftId = 0) or updates a shift and replaces its rider list.</summary>
    public record SaveShiftCommand : IRequest<Result<int>>
    {
        public int ShiftId { get; set; }
        public int CityId { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>Local (Egypt) time "HH:mm".</summary>
        public string StartTime { get; set; } = string.Empty;

        /// <summary>Local (Egypt) time "HH:mm"; earlier than StartTime crosses midnight.</summary>
        public string EndTime { get; set; } = string.Empty;

        /// <summary>Bitmask over DayOfWeek: Sunday = 1 … Saturday = 64. 127 = every day.</summary>
        public int DaysOfWeekMask { get; set; } = Domain.Models.Shift.AllDays;
        public bool IsActive { get; set; } = true;
        public List<int> DeliveryIds { get; set; } = new();
    }

    public class SaveShiftCommandHandler : IRequestHandler<SaveShiftCommand, Result<int>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;

        public SaveShiftCommandHandler(DatabaseContext context, IUserSession userSession)
        {
            _context = context;
            _userSession = userSession;
        }

        public async Task<Result<int>> Handle(SaveShiftCommand request, CancellationToken cancellationToken)
        {
            if (!ShiftMapper.TryParseTime(request.StartTime, out var start))
                return Result.Failure<int>("Start time must be in HH:mm format");
            if (!ShiftMapper.TryParseTime(request.EndTime, out var end))
                return Result.Failure<int>("End time must be in HH:mm format");

            var cityExists = await _context.Cities.AsNoTracking().AnyAsync(c => c.CityId == request.CityId, cancellationToken);
            if (!cityExists)
                return Result.Failure<int>($"City with ID {request.CityId} not found");

            var riderIds = (request.DeliveryIds ?? new List<int>()).Distinct().ToList();
            if (riderIds.Count > 0)
            {
                var riders = await _context.Deliveries
                    .AsNoTracking()
                    .Where(d => riderIds.Contains(d.DeliveryId) && !d.IsDeleted)
                    .Select(d => new { d.DeliveryId, d.CityId })
                    .ToListAsync(cancellationToken);

                if (riders.Count != riderIds.Count)
                    return Result.Failure<int>("One or more riders were not found");
                if (riders.Any(r => r.CityId != request.CityId))
                    return Result.Failure<int>("All riders must belong to the shift's city");
            }

            var actor = _userSession.UserName ?? "System";
            Domain.Models.Shift shift;

            try
            {
                if (request.ShiftId > 0)
                {
                    var existing = await _context.Shifts
                        .AsTracking()
                        .Include(s => s.DeliveryShifts)
                        .FirstOrDefaultAsync(s => s.ShiftId == request.ShiftId && !s.IsDeleted, cancellationToken);
                    if (existing == null)
                        return Result.Failure<int>($"Shift with ID {request.ShiftId} not found");

                    existing.Update(request.CityId, request.Name, start, end, request.DaysOfWeekMask, request.IsActive, actor);
                    shift = existing;

                    var toRemove = existing.DeliveryShifts.Where(ds => !riderIds.Contains(ds.DeliveryId)).ToList();
                    _context.DeliveryShifts.RemoveRange(toRemove);

                    var current = existing.DeliveryShifts.Select(ds => ds.DeliveryId).ToHashSet();
                    foreach (var riderId in riderIds.Where(id => !current.Contains(id)))
                        _context.DeliveryShifts.Add(DeliveryShift.Create(riderId, existing.ShiftId, actor));
                }
                else
                {
                    shift = Domain.Models.Shift.Create(request.CityId, request.Name, start, end, request.DaysOfWeekMask, request.IsActive, actor);
                    _context.Shifts.Add(shift);
                    await _context.SaveChangesAsync(cancellationToken);

                    foreach (var riderId in riderIds)
                        _context.DeliveryShifts.Add(DeliveryShift.Create(riderId, shift.ShiftId, actor));
                }
            }
            catch (ArgumentException ex)
            {
                return Result.Failure<int>(ex.Message);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success(shift.ShiftId);
        }
    }
}
